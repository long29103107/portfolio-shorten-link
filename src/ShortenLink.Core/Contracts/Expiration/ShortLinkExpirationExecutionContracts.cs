using ShortenLink.Core.Services;

namespace ShortenLink.Core.Contracts.Expiration;

public sealed record ShortLinkExpirationCheckpoint(
    string? TenantId,
    string? Cursor,
    DateTimeOffset EvaluatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record ShortLinkExpirationExecutionRequest(
    DateTimeOffset EvaluatedAtUtc,
    string? TenantId = null,
    int Limit = ShortLinkExpirationEvaluator.DefaultLimit,
    ShortLinkRetentionPolicy? RetentionPolicy = null,
    bool ResumeFromCheckpoint = true);

public sealed record ShortLinkExpirationExecutionResult(
    ShortLinkExpirationBatchResult Batch,
    string? PreviousCursor,
    string? CheckpointCursor,
    bool CheckpointAdvanced,
    int CacheInvalidationHandoffs);
