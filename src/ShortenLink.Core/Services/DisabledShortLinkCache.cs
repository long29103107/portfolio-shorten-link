using ShortenLink.Core.Domain;

namespace ShortenLink.Core.Services;

public sealed class DisabledShortLinkCache : IShortLinkCache, ITenantAwareShortLinkCache
{
    public Task<ShortLink?> FindByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        Task.FromResult<ShortLink?>(null);

    public Task SetAsync(ShortLink shortLink, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shortLink);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string code, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<ShortLink?> FindByCodeAsync(
        string code,
        string tenantId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<ShortLink?>(null);

    public Task RemoveAsync(
        string code,
        string tenantId,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
