using ShortenLink.Application.Services;
using ShortenLink.Core.Abstractions;
using ShortenLink.Core.Contracts.Expiration;
using ShortenLink.Core.Exceptions;
using Xunit;

namespace ShortenLink.Application.Tests;

public sealed class ShortLinkExpirationExecutionServiceTests
{
    [Fact]
    public async Task ExecuteBatchAsync_ResumesFromCheckpointAndHandsOffBeforeSaving()
    {
        var evaluatedAt = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
        var checkpoint = new InMemoryCheckpointRepository
        {
            Value = new ShortLinkExpirationCheckpoint(
                "tenant-a", "before-cursor", evaluatedAt, evaluatedAt.AddMinutes(-1))
        };
        var evaluator = new CapturingExpirationService();
        var invalidator = new CapturingInvalidationSink();
        var service = new ShortLinkExpirationExecutionService(
            evaluator,
            checkpoint,
            invalidator,
            TimeProvider.System);

        var result = await service.ExecuteBatchAsync(
            new ShortLinkExpirationExecutionRequest(
                evaluatedAt,
                TenantId: "tenant-a",
                Limit: 10));

        Assert.Equal("before-cursor", evaluator.Request!.Cursor);
        Assert.Equal("tenant-a", evaluator.Request.TenantId);
        Assert.Equal("expired01", Assert.Single(invalidator.Codes));
        Assert.Equal("next-cursor", checkpoint.Value!.Cursor);
        Assert.True(result.CheckpointAdvanced);
        Assert.Equal(1, result.CacheInvalidationHandoffs);
    }

    [Fact]
    public async Task ExecuteBatchAsync_DoesNotAdvanceCheckpointWhenHandoffFails()
    {
        var evaluatedAt = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
        var checkpoint = new InMemoryCheckpointRepository
        {
            Value = new ShortLinkExpirationCheckpoint(
                "tenant-a", "before-cursor", evaluatedAt, evaluatedAt)
        };
        var invalidator = new CapturingInvalidationSink { Accept = false };
        var service = new ShortLinkExpirationExecutionService(
            new CapturingExpirationService(),
            checkpoint,
            invalidator,
            TimeProvider.System);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ExecuteBatchAsync(new ShortLinkExpirationExecutionRequest(evaluatedAt, "tenant-a")));

        Assert.Equal("before-cursor", checkpoint.Value!.Cursor);
        Assert.False(checkpoint.WasSaved);
    }

    private sealed class CapturingExpirationService : IShortLinkExpirationService
    {
        public ShortLinkExpirationBatchRequest? Request { get; private set; }

        public Task<ShortLinkExpirationBatchResult> EvaluateBatchAsync(
            ShortLinkExpirationBatchRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(new ShortLinkExpirationBatchResult(
                [new ShortLinkExpirationEvaluation(
                    "expired01", request.TenantId, request.EvaluatedAtUtc.AddHours(-1),
                    request.EvaluatedAtUtc, ShortLinkExpirationOutcome.Expired,
                    "retention_elapsed", true)],
                "next-cursor",
                true));
        }
    }

    private sealed class InMemoryCheckpointRepository : IShortLinkExpirationCheckpointRepository
    {
        public ShortLinkExpirationCheckpoint? Value { get; set; }

        public bool WasSaved { get; private set; }

        public Task<ShortLinkExpirationCheckpoint?> FindAsync(
            string? tenantId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Value);

        public Task SaveAsync(
            ShortLinkExpirationCheckpoint checkpoint,
            CancellationToken cancellationToken = default)
        {
            Value = checkpoint;
            WasSaved = true;
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingInvalidationSink : IShortLinkExpirationCacheInvalidationSink
    {
        public bool Accept { get; init; } = true;

        public List<string> Codes { get; } = [];

        public Task<bool> TryInvalidateAsync(
            ShortLinkExpirationEvaluation evaluation,
            CancellationToken cancellationToken = default)
        {
            Codes.Add(evaluation.Code);
            return Task.FromResult(Accept);
        }
    }
}
