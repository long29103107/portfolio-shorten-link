using ShortenLink.Core.Domain;

namespace ShortenLink.Core.Abstractions;

public interface IShortLinkCache
{
    Task<ShortLink?> FindByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task SetAsync(ShortLink shortLink, CancellationToken cancellationToken = default);

    Task RemoveAsync(string code, CancellationToken cancellationToken = default);
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
