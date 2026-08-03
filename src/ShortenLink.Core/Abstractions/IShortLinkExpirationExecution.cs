using ShortenLink.Core.Contracts.Expiration;

namespace ShortenLink.Core.Abstractions;

public interface IShortLinkExpirationCheckpointRepository
{
    Task<ShortLinkExpirationCheckpoint?> FindAsync(
        string? tenantId,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        ShortLinkExpirationCheckpoint checkpoint,
        CancellationToken cancellationToken = default);
}

public interface IShortLinkExpirationCacheInvalidationSink
{
    Task<bool> TryInvalidateAsync(
        ShortLinkExpirationEvaluation evaluation,
        CancellationToken cancellationToken = default);
}

public interface IShortLinkExpirationExecutionService
{
    Task<ShortLinkExpirationExecutionResult> ExecuteBatchAsync(
        ShortLinkExpirationExecutionRequest request,
        CancellationToken cancellationToken = default);
}
