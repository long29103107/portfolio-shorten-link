using ShortenLink.Core.Abstractions;
using ShortenLink.Core.Contracts.Expiration;

namespace ShortenLink.Hosting;

internal sealed class ShortLinkExpirationCacheInvalidationSink(IShortLinkCache cache)
    : IShortLinkExpirationCacheInvalidationSink
{
    public async Task<bool> TryInvalidateAsync(
        ShortLinkExpirationEvaluation evaluation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evaluation);

        if (!evaluation.CacheInvalidationRequired)
        {
            return true;
        }

        if (evaluation.TenantId is null)
        {
            await cache.RemoveAsync(evaluation.Code, cancellationToken);
            return true;
        }

        if (cache is not ITenantAwareShortLinkCache tenantAwareCache)
        {
            return false;
        }

        await tenantAwareCache.RemoveAsync(
            evaluation.Code,
            evaluation.TenantId,
            cancellationToken);
        return true;
    }
}
