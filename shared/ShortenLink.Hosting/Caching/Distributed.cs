using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using ShortenLink.Core.Domain;
using ShortenLink.Core.Services;

namespace ShortenLink.Hosting;

internal sealed class DistributedCache : IShortLinkCache, ITenantAwareShortLinkCache, IShortLinkCacheLoader
{
    private const string KeyPrefix = "short-links:resolve:";
    private const string NegativeCacheMarker = "__shortenlink_negative__";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IDistributedCache distributedCache;
    private readonly TimeProvider timeProvider;
    private readonly IOptions<ShortenLinkOptions> options;
    private readonly ConcurrentDictionary<string, Lazy<Task<ShortLink?>>> inFlightLoads = new();

    public DistributedCache(
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

        return Deserialize(cachedJson);
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
        RemoveByKeyAsync(BuildKey(code), cancellationToken);

    public Task RemoveAsync(
        string code,
        string tenantId,
        CancellationToken cancellationToken = default) =>
        RemoveByKeyAsync(BuildTenantKey(code, tenantId), cancellationToken);

    private async Task RemoveByKeyAsync(string key, CancellationToken cancellationToken)
    {
        inFlightLoads.TryRemove(key, out _);
        await distributedCache.RemoveAsync(key, cancellationToken);
    }

    public Task<ShortLink?> GetOrCreateAsync(
        string code,
        Func<CancellationToken, Task<ShortLink?>> loader,
        CancellationToken cancellationToken = default) =>
        GetOrCreateByKeyAsync(BuildKey(code), loader, cancellationToken);

    public Task<ShortLink?> GetOrCreateAsync(
        string code,
        string tenantId,
        Func<CancellationToken, Task<ShortLink?>> loader,
        CancellationToken cancellationToken = default) =>
        GetOrCreateByKeyAsync(BuildTenantKey(code, tenantId), loader, cancellationToken);

    private async Task<ShortLink?> GetOrCreateByKeyAsync(
        string key,
        Func<CancellationToken, Task<ShortLink?>> loader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(loader);

        var cachedJson = await distributedCache.GetStringAsync(key, cancellationToken);
        if (!string.IsNullOrWhiteSpace(cachedJson))
        {
            if (string.Equals(cachedJson, NegativeCacheMarker, StringComparison.Ordinal))
            {
                return null;
            }

            var cached = Deserialize(cachedJson);
            if (cached is not null)
            {
                return cached;
            }
        }

        var lazyLoad = inFlightLoads.GetOrAdd(
            key,
            _ => new Lazy<Task<ShortLink?>>(
                () => LoadAndCacheAsync(key, loader),
                LazyThreadSafetyMode.ExecutionAndPublication));
        try
        {
            return await lazyLoad.Value.WaitAsync(cancellationToken);
        }
        finally
        {
            if (lazyLoad.IsValueCreated && lazyLoad.Value.IsCompleted)
            {
                inFlightLoads.TryRemove(new KeyValuePair<string, Lazy<Task<ShortLink?>>>(key, lazyLoad));
            }
        }
    }

    private async Task<ShortLink?> LoadAndCacheAsync(
        string key,
        Func<CancellationToken, Task<ShortLink?>> loader)
    {
        var shortLink = await loader(CancellationToken.None);
        if (shortLink is null)
        {
            await distributedCache.SetStringAsync(
                key,
                NegativeCacheMarker,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(
                        options.Value.Cache.NegativeEntryTtlSeconds)
                },
                CancellationToken.None);
            return null;
        }

        await SetAsync(shortLink, CancellationToken.None);
        return shortLink;
    }

    private async Task<ShortLink?> FindByKeyAsync(
        string key,
        CancellationToken cancellationToken)
    {
        var cachedJson = await distributedCache.GetStringAsync(key, cancellationToken);
        if (string.IsNullOrWhiteSpace(cachedJson))
        {
            return null;
        }

        return Deserialize(cachedJson);
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

    private static ShortLink? Deserialize(string cachedJson)
    {
        if (string.Equals(cachedJson, NegativeCacheMarker, StringComparison.Ordinal))
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

    private sealed record CachedShortLink(
        string Code,
        string OriginalUrl,
        DateTimeOffset CreatedAt,
        DateTimeOffset? ExpiresAt,
        bool IsActive,
        string? TenantId = null);
}
