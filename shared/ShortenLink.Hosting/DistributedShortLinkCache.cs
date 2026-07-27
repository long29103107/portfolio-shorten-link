using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using ShortenLink.Core.Domain;
using ShortenLink.Core.Services;

namespace ShortenLink.Hosting;

internal sealed class DistributedShortLinkCache : IShortLinkCache
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
                cached.IsActive);
    }

    public async Task SetAsync(
        ShortLink shortLink,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shortLink);

        if (!shortLink.CanResolve(timeProvider.GetUtcNow()))
        {
            await RemoveAsync(shortLink.Code, cancellationToken);
            return;
        }

        var cached = new CachedShortLink(
            shortLink.Code,
            shortLink.OriginalUrl.AbsoluteUri,
            shortLink.CreatedAt,
            shortLink.ExpiresAt,
            shortLink.IsActive);

        await distributedCache.SetStringAsync(
            BuildKey(shortLink.Code),
            JsonSerializer.Serialize(cached, SerializerOptions),
            CreateCacheOptions(shortLink),
            cancellationToken);
    }

    public Task RemoveAsync(
        string code,
        CancellationToken cancellationToken = default) =>
        distributedCache.RemoveAsync(BuildKey(code), cancellationToken);

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

    private sealed record CachedShortLink(
        string Code,
        string OriginalUrl,
        DateTimeOffset CreatedAt,
        DateTimeOffset? ExpiresAt,
        bool IsActive);
}
