using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShortenLink.Application.Abstractions;
using ShortenLink.Application.Contracts.Responses;
using ShortenLink.Application.Features.ShortLinks.Bulk;
using ShortenLink.Core.Exceptions;

namespace ShortenLink.Hosting;

public sealed class ShortLinkBulkJobScheduler
    : IShortLinkBulkJobScheduler
{
    private const int Capacity = 32;
    private const int MaxRetainedJobs = 256;
    private readonly Channel<WorkItem> _queue = Channel.CreateBounded<WorkItem>(new BoundedChannelOptions(Capacity)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
        SingleWriter = false
    });
    private readonly ConcurrentDictionary<Guid, JobState> _jobs = new();

    public ShortLinkBulkJobAcceptedResponse Enqueue(ExecuteShortLinkBulkOperationCommand request, CurrentRequestActor actor)
    {
        var job = new JobState(Guid.NewGuid(), request, actor);
        _jobs[job.Id] = job;
        if (!_queue.Writer.TryWrite(new WorkItem(job.Id)))
        {
            _jobs.TryRemove(job.Id, out _);
            throw new ConflictException("bulk_job_queue_full", "Bulk job queue is full. Try again later.");
        }
        TrimCompletedJobs();
        return new ShortLinkBulkJobAcceptedResponse(job.Id, job.Status, job.TotalCount);
    }

    public ShortLinkBulkJobStatusResponse GetStatus(Guid jobId, CurrentRequestActor actor)
    {
        if (!_jobs.TryGetValue(jobId, out var job) || !job.IsOwnedBy(actor))
            throw new NotFoundException(ErrorCodes.NotFound, "Bulk job was not found.");
        return job.Snapshot();
    }

    internal async Task RunAsync(IServiceScopeFactory scopeFactory, CancellationToken cancellationToken)
    {
        await foreach (var work in _queue.Reader.ReadAllAsync(cancellationToken))
        {
            if (!_jobs.TryGetValue(work.JobId, out var job))
                continue;

            job.MarkRunning();
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var executor = scope.ServiceProvider.GetRequiredService<ShortLinkBulkOperationExecutor>();
                var result = await executor.ExecuteAsync(
                    job.Request,
                    job.Actor,
                    cancellationToken,
                    processed => job.MarkProcessed(processed));
                job.MarkCompleted(result);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                job.MarkFailed("Bulk job stopped before completion.");
            }
            catch (Exception exception)
            {
                job.MarkFailed(exception.Message);
            }
        }
    }

    private void TrimCompletedJobs()
    {
        if (_jobs.Count <= MaxRetainedJobs)
            return;

        foreach (var candidate in _jobs.Values
                     .Where(static job => job.IsTerminal)
                     .OrderBy(static job => job.CreatedAtUtc)
                     .Take(_jobs.Count - MaxRetainedJobs))
            _jobs.TryRemove(candidate.Id, out _);
    }

    private readonly record struct WorkItem(Guid JobId);

    private sealed class JobState(Guid id, ExecuteShortLinkBulkOperationCommand request, CurrentRequestActor actor)
    {
        private readonly object _sync = new();
        private string _status = ShortLinkBulkJobStatuses.Queued;
        private int _processedCount;
        private int _succeededCount;
        private int _failedCount;
        private ShortLinkBulkOperationResponse? _result;
        private string? _error;

        public Guid Id { get; } = id;
        public ExecuteShortLinkBulkOperationCommand Request { get; } = request;
        public CurrentRequestActor Actor { get; } = actor;
        public DateTimeOffset CreatedAtUtc { get; } = DateTimeOffset.UtcNow;
        public int TotalCount => Request.Codes?.Count ?? 0;
        public string Status { get { lock (_sync) return _status; } }
        public bool IsTerminal { get { lock (_sync) return _status is ShortLinkBulkJobStatuses.Completed or ShortLinkBulkJobStatuses.Failed; } }

        public bool IsOwnedBy(CurrentRequestActor actor) =>
            string.Equals(Actor.TenantId, actor.TenantId, StringComparison.Ordinal)
            && string.Equals(OwnerKey(Actor), OwnerKey(actor), StringComparison.Ordinal);

        public void MarkRunning() { lock (_sync) _status = ShortLinkBulkJobStatuses.Running; }

        public void MarkProcessed(int processed)
        {
            lock (_sync) _processedCount = Math.Clamp(processed, 0, TotalCount);
        }

        public void MarkCompleted(ShortLinkBulkOperationResponse result)
        {
            lock (_sync)
            {
                _result = result;
                _processedCount = result.RequestedCount;
                _succeededCount = result.SucceededCount;
                _failedCount = result.FailedCount;
                _status = ShortLinkBulkJobStatuses.Completed;
            }
        }

        public void MarkFailed(string error)
        {
            lock (_sync)
            {
                _error = error;
                _status = ShortLinkBulkJobStatuses.Failed;
            }
        }

        public ShortLinkBulkJobStatusResponse Snapshot()
        {
            lock (_sync)
                return new ShortLinkBulkJobStatusResponse(Id, _status, TotalCount, _processedCount, _succeededCount, _failedCount, _result, _error);
        }

        private static string OwnerKey(CurrentRequestActor actor) => actor.ActorId ?? actor.UserId ?? (actor.IsAdmin ? "admin" : "anonymous");
    }
}

internal sealed class ShortLinkBulkJobBackgroundService(
    ShortLinkBulkJobScheduler scheduler,
    IServiceScopeFactory scopeFactory,
    ILogger<ShortLinkBulkJobBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await scheduler.RunAsync(scopeFactory, stoppingToken);
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
