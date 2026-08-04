using ShortenLink.Core.Domain;

namespace ShortenLink.Core.Abstractions;

public interface IShortLinkCache
{
    Task<ShortLink?> FindByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task SetAsync(ShortLink shortLink, CancellationToken cancellationToken = default);

    Task RemoveAsync(string code, CancellationToken cancellationToken = default);
}

/// <summary>
/// Optional cache capability that coalesces concurrent misses and can retain a
/// short-lived negative result. The loader is invoked only after the cache has
/// confirmed that the key is absent.
/// </summary>
public interface IShortLinkCacheLoader
{
    Task<ShortLink?> GetOrCreateAsync(
        string code,
        Func<CancellationToken, Task<ShortLink?>> loader,
        CancellationToken cancellationToken = default);

    Task<ShortLink?> GetOrCreateAsync(
        string code,
        string tenantId,
        Func<CancellationToken, Task<ShortLink?>> loader,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Optional cache capability for tenant-partitioned resolve entries.
/// Providers that do not implement this capability are bypassed for tenant-aware requests.
/// </summary>
public interface ITenantAwareShortLinkCache
{
    Task<ShortLink?> FindByCodeAsync(
        string code,
        string tenantId,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(
        string code,
        string tenantId,
        CancellationToken cancellationToken = default);
}
