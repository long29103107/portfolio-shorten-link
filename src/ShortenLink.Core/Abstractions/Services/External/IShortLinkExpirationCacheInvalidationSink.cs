using ShortenLink.Core.Contracts.Expiration;

namespace ShortenLink.Core.Abstractions;

public interface IShortLinkExpirationCacheInvalidationSink
{
    Task<bool> TryInvalidateAsync(
        ShortLinkExpirationEvaluation evaluation,
        CancellationToken cancellationToken = default);
}
