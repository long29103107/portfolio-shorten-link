using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using ShortenLink.Core.Domain;
using ShortenLink.Core.Services;

namespace ShortenLink.Hosting;

internal sealed class DistributedShortLinkCache : IShortLinkCache, ITenantAwareShortLinkCache
{
    private const string KeyPrefix = "short-links:resolve:";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IDistributedCache distributedCache;
    private readonly TimeProvider timeProvider;
    private readonly IOptions<ShortenLinkOptions> options;

    public DistributedShortLinkCache(
        IDistributedCache distributedCache,
        TimeProvider timeProvider,
        IOptions<ShortenLinkOptions> options)
    {
        this.distributedCache = distributedCache ?? throw new ArgumentNullException(nameof(distributedCache));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<ShortLink?> FindByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var cachedJson = await distributedCache.GetStringAsync(
            BuildKey(code),
            cancellationToken);

        if (string.IsNullOrWhiteSpace(cachedJson))
        {
            return null;
        }

        var cached = JsonSerializer.Deserialize<CachedShortLink>(cachedJson, SerializerOptions);
        return cached is null
            ? null
            : new ShortLink(
                cached.Code,
                new Uri(cached.OriginalUrl, UriKind.Absolute),
                cached.CreatedAt,
                cached.ExpiresAt,
                cached.IsActive,
                tenantId: cached.TenantId);
    }

    public Task<ShortLink?> FindByCodeAsync(
        string code,
        string tenantId,
        CancellationToken cancellationToken = default) =>
        FindByKeyAsync(BuildTenantKey(code, tenantId), cancellationToken);

    public async Task SetAsync(
        ShortLink shortLink,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shortLink);

        if (!shortLink.CanResolve(timeProvider.GetUtcNow()))
        {
            if (shortLink.TenantId is null)
            {
                await RemoveAsync(shortLink.Code, cancellationToken);
            }
            else
            {
                await RemoveAsync(shortLink.Code, shortLink.TenantId, cancellationToken);
            }
            return;
        }

        var cached = new CachedShortLink(
            shortLink.Code,
            shortLink.OriginalUrl.AbsoluteUri,
            shortLink.CreatedAt,
            shortLink.ExpiresAt,
            shortLink.IsActive,
            shortLink.TenantId);

        await distributedCache.SetStringAsync(
            BuildKey(shortLink),
            JsonSerializer.Serialize(cached, SerializerOptions),
            CreateCacheOptions(shortLink),
            cancellationToken);
    }

    public Task RemoveAsync(
        string code,
        CancellationToken cancellationToken = default) =>
        distributedCache.RemoveAsync(BuildKey(code), cancellationToken);

    public Task RemoveAsync(
        string code,
        string tenantId,
        CancellationToken cancellationToken = default) =>
        distributedCache.RemoveAsync(BuildTenantKey(code, tenantId), cancellationToken);

    private async Task<ShortLink?> FindByKeyAsync(
        string key,
        CancellationToken cancellationToken)
    {
        var cachedJson = await distributedCache.GetStringAsync(key, cancellationToken);
        if (string.IsNullOrWhiteSpace(cachedJson))
        {
            return null;
        }

        var cached = JsonSerializer.Deserialize<CachedShortLink>(cachedJson, SerializerOptions);
        return cached is null
            ? null
            : new ShortLink(
                cached.Code,
                new Uri(cached.OriginalUrl, UriKind.Absolute),
                cached.CreatedAt,
                cached.ExpiresAt,
                cached.IsActive,
                tenantId: cached.TenantId);
    }

    private DistributedCacheEntryOptions CreateCacheOptions(ShortLink shortLink)
    {
        var cacheOptions = new DistributedCacheEntryOptions();
        if (shortLink.ExpiresAt is not null)
        {
            cacheOptions.AbsoluteExpiration = shortLink.ExpiresAt;
            return cacheOptions;
        }

        cacheOptions.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(options.Value.Cache.EntryTtlSeconds);
        return cacheOptions;
    }

    private static string BuildKey(string code) =>
        $"{KeyPrefix}{code.Trim()}";

    private static string BuildKey(ShortLink shortLink) =>
        shortLink.TenantId is null
            ? BuildKey(shortLink.Code)
            : BuildTenantKey(shortLink.Code, shortLink.TenantId);

    private static string BuildTenantKey(string code, string tenantId) =>
        $"{KeyPrefix}tenant:{tenantId.Trim()}:{code.Trim()}";

    private sealed record CachedShortLink(
        string Code,
        string OriginalUrl,
        DateTimeOffset CreatedAt,
        DateTimeOffset? ExpiresAt,
        bool IsActive,
        string? TenantId = null);
}
