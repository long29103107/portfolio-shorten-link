using ShortenLink.Core.Contracts.Expiration;

namespace ShortenLink.Core.Abstractions;

public interface IShortLinkExpirationExecutionService
{
    Task<ShortLinkExpirationExecutionResult> ExecuteBatchAsync(
        ShortLinkExpirationExecutionRequest request,
        CancellationToken cancellationToken = default);
}
