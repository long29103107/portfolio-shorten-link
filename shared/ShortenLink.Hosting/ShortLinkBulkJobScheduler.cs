using System.Text.Json;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShortenLink.Application.Abstractions;
using ShortenLink.Application.Contracts.Responses;
using ShortenLink.Application.Features.ShortLinks.Bulk;
using ShortenLink.Core.Exceptions;
using ShortenLink.Infrastructure.Persistence;
using ShortenLink.Infrastructure.Persistence.Entities;

namespace ShortenLink.Hosting;

public sealed class ShortLinkBulkJobScheduler(
    IServiceScopeFactory scopeFactory,
    IOptions<ShortenLinkOptions> options)
    : IShortLinkBulkJobScheduler
{
    private const int Capacity = 32;
    private readonly Channel<Guid> queue = Channel.CreateBounded<Guid>(new BoundedChannelOptions(Capacity)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
        SingleWriter = false
    });
    private readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ShortenLinkBulkJobOptions jobOptions = options.Value.BulkJobs;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> runningJobs = new();

    public async Task<ShortLinkBulkJobAcceptedResponse> EnqueueAsync(
        ExecuteShortLinkBulkOperationCommand request,
        CurrentRequestActor actor,
        CancellationToken cancellationToken = default,
        string? idempotencyKey = null)
    {
        var normalizedKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
        var requestHash = BuildRequestHash(request);
        if (normalizedKey is not null)
        {
            await using var lookupScope = scopeFactory.CreateAsyncScope();
            var lookupDb = lookupScope.ServiceProvider.GetRequiredService<ShortLinkDbContext>();
            var existing = await lookupDb.ShortLinkBulkJobs.AsNoTracking()
                .SingleOrDefaultAsync(job => job.TenantId == (actor.TenantId ?? string.Empty)
                    && job.IdempotencyKey == normalizedKey, cancellationToken);
            if (existing is not null)
            {
                if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
                    throw new ConflictException("idempotency_key_reused", "Idempotency key was already used for a different bulk request.");
                return new ShortLinkBulkJobAcceptedResponse(existing.Id, existing.Status, existing.TotalCount);
            }
        }

        var jobId = Guid.CreateVersion7();
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShortLinkDbContext>();
            db.ShortLinkBulkJobs.Add(ToEntity(jobId, request, actor, normalizedKey, requestHash));
            await db.SaveChangesAsync(cancellationToken);
        }

        if (!queue.Writer.TryWrite(jobId))
        {
            await DeleteAsync(jobId, cancellationToken);
            throw new ConflictException("bulk_job_queue_full", "Bulk job queue is full. Try again later.");
        }

        BulkJobMetrics.Submitted.Add(1);
        BulkJobMetrics.QueueDepth.Add(1);
        return new ShortLinkBulkJobAcceptedResponse(jobId, ShortLinkBulkJobStatuses.Queued, request.Codes?.Count ?? 0);
    }

    public async Task<ShortLinkBulkJobStatusResponse> GetStatusAsync(
        Guid jobId,
        CurrentRequestActor actor,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ShortLinkDbContext>();
        var job = await db.ShortLinkBulkJobs.AsNoTracking().SingleOrDefaultAsync(item => item.Id == jobId, cancellationToken);
        if (job is null || !Owns(job, actor))
            throw new NotFoundException(ErrorCodes.NotFound, "Bulk job was not found.");
        return ToResponse(job);
    }

    public async Task<ShortLinkBulkJobStatusResponse> CancelAsync(
        Guid jobId,
        CurrentRequestActor actor,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ShortLinkDbContext>();
        var job = await db.ShortLinkBulkJobs.SingleOrDefaultAsync(item => item.Id == jobId, cancellationToken);
        if (job is null || !Owns(job, actor))
            throw new NotFoundException(ErrorCodes.NotFound, "Bulk job was not found.");
        if (job.Status is ShortLinkBulkJobStatuses.Completed or ShortLinkBulkJobStatuses.Failed or ShortLinkBulkJobStatuses.Cancelled)
            throw new ConflictException("bulk_job_not_cancellable", "Only queued or running bulk jobs can be cancelled.");

        job.CancellationRequested = true;
        if (job.Status == ShortLinkBulkJobStatuses.Queued)
        {
            job.Status = ShortLinkBulkJobStatuses.Cancelled;
            job.CompletedAtUtc = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(cancellationToken);
        if (runningJobs.TryGetValue(jobId, out var cancellationSource))
            cancellationSource.Cancel();
        return ToResponse(job);
    }

    internal async Task RunAsync(CancellationToken cancellationToken)
    {
        await RecoverAndQueueAsync(cancellationToken);
        await foreach (var jobId in queue.Reader.ReadAllAsync(cancellationToken))
        {
            BulkJobMetrics.QueueDepth.Add(-1);
            await ProcessAsync(jobId, cancellationToken);
        }
    }

    private async Task RecoverAndQueueAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ShortLinkDbContext>();
        var interrupted = await db.ShortLinkBulkJobs
            .Where(job => job.Status == ShortLinkBulkJobStatuses.Running)
            .ToListAsync(cancellationToken);
        foreach (var job in interrupted)
        {
            job.Status = ShortLinkBulkJobStatuses.Failed;
            job.Error = "Bulk job was interrupted by an application restart.";
            job.CompletedAtUtc = DateTimeOffset.UtcNow;
        }

        var queued = await db.ShortLinkBulkJobs
            .Where(job => job.Status == ShortLinkBulkJobStatuses.Queued)
            .OrderBy(job => job.CreatedAt)
            .Take(Capacity)
            .Select(job => job.Id)
            .ToListAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        foreach (var jobId in queued)
            queue.Writer.TryWrite(jobId);
    }

    private async Task ProcessAsync(Guid jobId, CancellationToken cancellationToken)
    {
        ExecuteShortLinkBulkOperationCommand? request = null;
        CurrentRequestActor? actor = null;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShortLinkDbContext>();
            var job = await db.ShortLinkBulkJobs.SingleOrDefaultAsync(item => item.Id == jobId, cancellationToken);
            if (job is null || job.Status != ShortLinkBulkJobStatuses.Queued)
                return;
            job.Status = ShortLinkBulkJobStatuses.Running;
            job.AttemptCount++;
            job.StartedAtUtc ??= DateTimeOffset.UtcNow;
            job.LastHeartbeatAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            request = FromRequest(job);
            actor = new CurrentRequestActor(job.UserId, job.IsAdmin, job.ActorId, job.TenantId);
        }

        using var jobCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        runningJobs[jobId] = jobCancellationSource;
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var executor = scope.ServiceProvider.GetRequiredService<ShortLinkBulkOperationExecutor>();
            var result = await executor.ExecuteAsync(request!, actor!, jobCancellationSource.Token, processed =>
            {
                _ = PersistProgressAsync(jobId, processed, jobCancellationSource.Token);
            });
            await CompleteAsync(jobId, result, jobCancellationSource.Token);
        }
        catch (OperationCanceledException) when (jobCancellationSource.IsCancellationRequested)
        {
            await MarkCancelledIfRequestedAsync(jobId);
        }
        catch (Exception exception)
        {
            await RetryOrFailAsync(jobId, exception, cancellationToken);
        }
        finally
        {
            runningJobs.TryRemove(jobId, out _);
        }
    }

    private async Task MarkCancelledIfRequestedAsync(Guid jobId)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ShortLinkDbContext>();
        var job = await db.ShortLinkBulkJobs.SingleOrDefaultAsync(item => item.Id == jobId);
        if (job is null) return;
        job.Status = job.CancellationRequested ? ShortLinkBulkJobStatuses.Cancelled : ShortLinkBulkJobStatuses.Failed;
        job.Error = job.CancellationRequested ? "Bulk job cancelled by the submitting actor." : "Bulk job stopped before completion.";
        job.CompletedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        BulkJobMetrics.Cancelled.Add(1);
    }

    private async Task RetryOrFailAsync(Guid jobId, Exception exception, CancellationToken cancellationToken)
    {
        if (!IsTransient(exception))
        {
            await FailAsync(jobId, exception.Message, CancellationToken.None);
            return;
        }

        var shouldRetry = false;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShortLinkDbContext>();
            var job = await db.ShortLinkBulkJobs.SingleOrDefaultAsync(item => item.Id == jobId, CancellationToken.None);
            if (job is null) return;
            shouldRetry = job.AttemptCount < jobOptions.MaxAttempts;
            if (shouldRetry)
            {
                job.Status = ShortLinkBulkJobStatuses.Queued;
                job.Error = $"Transient failure; retry {job.AttemptCount + 1} of {jobOptions.MaxAttempts}.";
                await db.SaveChangesAsync(CancellationToken.None);
            }
        }

        if (!shouldRetry)
        {
            await FailAsync(jobId, exception.Message, CancellationToken.None);
            return;
        }

        await Task.Delay(jobOptions.RetryDelayMilliseconds, cancellationToken);
        queue.Writer.TryWrite(jobId);
        BulkJobMetrics.Retried.Add(1);
        BulkJobMetrics.QueueDepth.Add(1);
    }

    private static bool IsTransient(Exception exception) =>
        exception is TimeoutException or IOException or DbUpdateException
        || exception.InnerException is TimeoutException or IOException;

    private async Task PersistProgressAsync(Guid jobId, int processed, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ShortLinkDbContext>();
            var job = await db.ShortLinkBulkJobs.SingleOrDefaultAsync(item => item.Id == jobId, cancellationToken);
            if (job is null || job.Status != ShortLinkBulkJobStatuses.Running)
                return;
            job.ProcessedCount = Math.Clamp(processed, 0, job.TotalCount);
            job.LastHeartbeatAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Progress persistence is best-effort; the operation result remains authoritative.
        }
    }

    private async Task CompleteAsync(Guid jobId, ShortLinkBulkOperationResponse result, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ShortLinkDbContext>();
        var job = await db.ShortLinkBulkJobs.SingleOrDefaultAsync(item => item.Id == jobId, cancellationToken);
        if (job is null) return;
        job.Status = ShortLinkBulkJobStatuses.Completed;
        job.ProcessedCount = result.RequestedCount;
        job.SucceededCount = result.SucceededCount;
        job.FailedCount = result.FailedCount;
        job.ResultJson = JsonSerializer.Serialize(result, jsonOptions);
        job.CompletedAtUtc = DateTimeOffset.UtcNow;
        job.LastHeartbeatAtUtc = job.CompletedAtUtc;
        await db.SaveChangesAsync(cancellationToken);
        BulkJobMetrics.Completed.Add(1);
    }

    private async Task FailAsync(Guid jobId, string error, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ShortLinkDbContext>();
        var job = await db.ShortLinkBulkJobs.SingleOrDefaultAsync(item => item.Id == jobId, cancellationToken);
        if (job is null) return;
        job.Status = ShortLinkBulkJobStatuses.Failed;
        job.Error = error;
        job.CompletedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        BulkJobMetrics.Failed.Add(1);
    }

    private async Task DeleteAsync(Guid jobId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ShortLinkDbContext>();
        var job = await db.ShortLinkBulkJobs.FindAsync([jobId], cancellationToken);
        if (job is not null)
        {
            db.ShortLinkBulkJobs.Remove(job);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private ShortLinkBulkJobPersistenceEntity ToEntity(Guid id, ExecuteShortLinkBulkOperationCommand request, CurrentRequestActor actor, string? idempotencyKey, string requestHash) => new(id)
    {
        Operation = ShortLinkBulkOperations.Normalize(request.Operation),
        CodesJson = JsonSerializer.Serialize(request.Codes ?? [], jsonOptions),
        Folder = request.Folder,
        TagsJson = JsonSerializer.Serialize(request.Tags ?? [], jsonOptions),
        Status = ShortLinkBulkJobStatuses.Queued,
        TotalCount = request.Codes?.Count ?? 0,
        ActorId = actor.ActorId,
        UserId = actor.UserId,
        IsAdmin = actor.IsAdmin,
        TenantId = actor.TenantId ?? string.Empty
        ,IdempotencyKey = idempotencyKey
        ,RequestHash = requestHash
    };

    private string BuildRequestHash(ExecuteShortLinkBulkOperationCommand request) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
            new { operation = ShortLinkBulkOperations.Normalize(request.Operation), codes = request.Codes?.Select(code => code.Trim()).ToArray(), request.Folder, tags = request.Tags?.ToArray() },
            jsonOptions))));

    private ExecuteShortLinkBulkOperationCommand FromRequest(ShortLinkBulkJobPersistenceEntity job) =>
        new(JsonSerializer.Deserialize<IReadOnlyList<string>>(job.CodesJson, jsonOptions) ?? [], job.Operation, job.Folder,
            JsonSerializer.Deserialize<IReadOnlyList<string>>(job.TagsJson, jsonOptions) ?? []);

    private ShortLinkBulkJobStatusResponse ToResponse(ShortLinkBulkJobPersistenceEntity job) =>
        new(job.Id, job.Status, job.TotalCount, job.ProcessedCount, job.SucceededCount, job.FailedCount,
            string.IsNullOrWhiteSpace(job.ResultJson)
                ? null
                : JsonSerializer.Deserialize<ShortLinkBulkOperationResponse>(job.ResultJson, jsonOptions),
            job.Error);

    private static bool Owns(ShortLinkBulkJobPersistenceEntity job, CurrentRequestActor actor) =>
        string.Equals(job.TenantId, actor.TenantId ?? string.Empty, StringComparison.Ordinal)
        && string.Equals(job.ActorId, actor.ActorId, StringComparison.Ordinal)
        && string.Equals(job.UserId, actor.UserId, StringComparison.Ordinal)
        && job.IsAdmin == actor.IsAdmin;
}

internal sealed class ShortLinkBulkJobBackgroundService(
    ShortLinkBulkJobScheduler scheduler,
    ILogger<ShortLinkBulkJobBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await scheduler.RunAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Bulk job worker stopped unexpectedly.");
        }
    }
}
