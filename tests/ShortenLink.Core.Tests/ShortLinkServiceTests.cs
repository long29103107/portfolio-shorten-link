using ShortenLink.Core.Domain;
using ShortenLink.Core.Generation;
using ShortenLink.Application.Services;
using ShortenLink.Core.Services;
using Xunit;

namespace ShortenLink.Core.Tests;

public sealed class ShortLinkServiceTests
{
    [Fact]
    public async Task CreateAsync_RejectsInvalidUrl()
    {
        var service = CreateService();

        var result = await service.CreateAsync(new CreateShortLinkRequest("ftp://example.com/file"));

        Assert.False(result.Succeeded);
        Assert.Equal(ShortLinkErrorCodes.InvalidUrl, result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_GeneratesUniqueDefaultCode()
    {
        var now = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        var repository = new InMemoryShortLinkRepository();
        await repository.AddAsync(new ShortLink("taken01", new Uri("https://example.com"), now));
        var service = CreateService(
            repository,
            new SequenceCodeGenerator("taken01", "fresh01"),
            timeProvider: new FixedTimeProvider(now));

        var result = await service.CreateAsync(
            new CreateShortLinkRequest(
                "https://openai.com",
                now.AddDays(1),
                "user-1",
                "Ada Lovelace",
                "ada@example.com"));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.ShortLink);
        Assert.Equal("fresh01", result.ShortLink.Code);
        Assert.Equal("user-1", result.ShortLink.CreatedByUserId);
        Assert.Equal("Ada Lovelace", result.ShortLink.CreatedByDisplayName);
        Assert.Equal("ada@example.com", result.ShortLink.CreatedByUsername);
    }

    [Fact]
    public async Task CreateAsync_RejectsMissingExpiration()
    {
        var service = CreateService();

        var result = await service.CreateAsync(new CreateShortLinkRequest("https://openai.com"));

        Assert.False(result.Succeeded);
        Assert.Equal(ShortLinkErrorCodes.InvalidExpiration, result.ErrorCode);
    }

    [Fact]
    public async Task ResolveAsync_RejectsExpiredLink()
    {
        var now = new DateTimeOffset(2026, 7, 11, 0, 0, 0, TimeSpan.Zero);
        var repository = new InMemoryShortLinkRepository();
        await repository.AddAsync(new ShortLink("expired", new Uri("https://example.com"), now.AddDays(-2), now.AddDays(-1)));
        var service = CreateService(repository, timeProvider: new FixedTimeProvider(now));

        var result = await service.ResolveAsync("expired");

        Assert.False(result.Succeeded);
        Assert.Equal(ShortLinkErrorCodes.Expired, result.ErrorCode);
    }

    [Fact]
    public async Task DeactivateAsync_MarksLinkInactive()
    {
        var repository = new InMemoryShortLinkRepository();
        await repository.AddAsync(new ShortLink("docs", new Uri("https://example.com"), DateTimeOffset.UtcNow));
        var service = CreateService(repository);

        var result = await service.DeactivateAsync("docs");
        var resolveResult = await service.ResolveAsync("docs");

        Assert.True(result.Succeeded);
        Assert.False(resolveResult.Succeeded);
        Assert.Equal(ShortLinkErrorCodes.Inactive, resolveResult.ErrorCode);
    }

    [Fact]
    public async Task UpdateAsync_ChangesDestinationAndClearsCache()
    {
        var now = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        var repository = new InMemoryShortLinkRepository();
        var cache = new InMemoryShortLinkCache();
        var shortLink = new ShortLink("edit001", new Uri("https://example.com/old"), now, now.AddDays(1));
        await repository.AddAsync(shortLink);
        await cache.SetAsync(shortLink);
        var service = CreateService(repository, cache: cache, timeProvider: new FixedTimeProvider(now));

        var result = await service.UpdateAsync(
            "edit001",
            new UpdateShortLinkRequest("https://example.com/new", now.AddDays(2)));

        Assert.True(result.Succeeded);
        Assert.Equal("https://example.com/new", result.ShortLink?.OriginalUrl.AbsoluteUri.TrimEnd('/'));
        Assert.Null(await cache.FindByCodeAsync("edit001"));
    }

    [Fact]
    public async Task UpdateAsync_RejectsMissingExpiration()
    {
        var repository = new InMemoryShortLinkRepository();
        await repository.AddAsync(new ShortLink("edit001", new Uri("https://example.com/old"), DateTimeOffset.UtcNow));
        var service = CreateService(repository);

        var result = await service.UpdateAsync(
            "edit001",
            new UpdateShortLinkRequest("https://example.com/new"));

        Assert.False(result.Succeeded);
        Assert.Equal(ShortLinkErrorCodes.InvalidExpiration, result.ErrorCode);
    }

    [Fact]
    public async Task DeleteAsync_RemovesStoredLinkAndCache()
    {
        var repository = new InMemoryShortLinkRepository();
        var cache = new InMemoryShortLinkCache();
        var shortLink = new ShortLink("delete1", new Uri("https://example.com/delete"), DateTimeOffset.UtcNow);
        await repository.AddAsync(shortLink);
        await cache.SetAsync(shortLink);
        var service = CreateService(repository, cache: cache);

        var result = await service.DeleteAsync("delete1");
        var details = await service.GetDetailsAsync("delete1");

        Assert.True(result.Succeeded);
        Assert.Equal(ShortLinkErrorCodes.NotFound, details.ErrorCode);
        Assert.Null(await cache.FindByCodeAsync("delete1"));
    }

    [Fact]
    public async Task ResolveAsync_UsesCacheBeforeRepository()
    {
        var repository = new InMemoryShortLinkRepository();
        var cache = new InMemoryShortLinkCache();
        await cache.SetAsync(new ShortLink("cached1", new Uri("https://example.com/cached"), DateTimeOffset.UtcNow));
        var service = CreateService(repository, cache: cache);

        var result = await service.ResolveAsync("cached1");

        Assert.True(result.Succeeded);
        Assert.Equal("https://example.com/cached", result.ShortLink?.OriginalUrl.AbsoluteUri.TrimEnd('/'));
        Assert.Equal(0, repository.FindByCodeCallCount);
    }

    [Fact]
    public async Task ResolveAsync_CachesSuccessfulRepositoryLookup()
    {
        var repository = new InMemoryShortLinkRepository();
        var cache = new InMemoryShortLinkCache();
        await repository.AddAsync(new ShortLink("cacheme", new Uri("https://example.com/db"), DateTimeOffset.UtcNow));
        var service = CreateService(repository, cache: cache);

        var first = await service.ResolveAsync("cacheme");
        var second = await service.ResolveAsync("cacheme");

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(1, repository.FindByCodeCallCount);
        Assert.NotNull(await cache.FindByCodeAsync("cacheme"));
    }

    [Fact]
    public async Task DeactivateAsync_RemovesCachedLink()
    {
        var repository = new InMemoryShortLinkRepository();
        var cache = new InMemoryShortLinkCache();
        var shortLink = new ShortLink("remove1", new Uri("https://example.com/remove"), DateTimeOffset.UtcNow);
        await repository.AddAsync(shortLink);
        await cache.SetAsync(shortLink);
        var service = CreateService(repository, cache: cache);

        var result = await service.DeactivateAsync("remove1");

        Assert.True(result.Succeeded);
        Assert.Null(await cache.FindByCodeAsync("remove1"));
    }

    private static ShortLinkService CreateService(
        InMemoryShortLinkRepository? repository = null,
        IShortCodeGenerator? generator = null,
        IShortLinkCache? cache = null,
        TimeProvider? timeProvider = null)
    {
        return new ShortLinkService(
            repository ?? new InMemoryShortLinkRepository(),
            generator ?? new SequenceCodeGenerator("abc1234"),
            cache,
            timeProvider);
    }

    private sealed class InMemoryShortLinkRepository : IShortLinkRepository
    {
        private readonly Dictionary<string, ShortLink> links = new(StringComparer.Ordinal);

        public int FindByCodeCallCount { get; private set; }

        public Task<IReadOnlyList<ShortLink>> ListRecentAsync(
            int limit,
            DateTimeOffset? beforeCreatedAt = null,
            string? beforeCode = null,
            CancellationToken cancellationToken = default)
        {
            var result = links.Values
                .OrderByDescending(link => link.CreatedAt)
                .ThenBy(link => link.Code, StringComparer.Ordinal)
                .Where(link =>
                    beforeCreatedAt is null
                    || link.CreatedAt < beforeCreatedAt
                    || (link.CreatedAt == beforeCreatedAt
                        && !string.IsNullOrWhiteSpace(beforeCode)
                        && string.Compare(link.Code, beforeCode, StringComparison.Ordinal) > 0))
                .Take(limit)
                .ToList();

            return Task.FromResult<IReadOnlyList<ShortLink>>(result);
        }

        public Task<IReadOnlyList<ShortLink>> ListRecentPageAsync(
            int skip,
            int limit,
            CancellationToken cancellationToken = default)
        {
            var result = links.Values
                .OrderByDescending(link => link.CreatedAt)
                .ThenBy(link => link.Code, StringComparer.Ordinal)
                .Skip(skip)
                .Take(limit)
                .ToList();

            return Task.FromResult<IReadOnlyList<ShortLink>>(result);
        }

        public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(links.Count);

        public Task<ShortLinkListPage> ListPageAsync(
            int skip,
            int limit,
            ShortLinkListQuery query,
            CancellationToken cancellationToken = default)
        {
            var filtered = links.Values
                .Where(link => string.IsNullOrWhiteSpace(query.Search)
                    || link.Code.Contains(query.Search, StringComparison.OrdinalIgnoreCase)
                    || link.OriginalUrl.AbsoluteUri.Contains(query.Search, StringComparison.OrdinalIgnoreCase))
                .Where(link => query.Status switch
                {
                    ShortLinkListStatus.Active => link.IsActive && !link.IsExpired(query.Now),
                    ShortLinkListStatus.Inactive => !link.IsActive,
                    ShortLinkListStatus.Expired => link.IsActive && link.IsExpired(query.Now),
                    ShortLinkListStatus.ExpiringSoon => link.IsActive
                        && !link.IsExpired(query.Now)
                        && link.ExpiresAt is not null
                        && link.ExpiresAt <= query.ExpiringSoonBefore,
                    _ => true
                })
                .ToList();

            var sorted = query.SortBy switch
            {
                ShortLinkListSortBy.Expiry => ApplyDirection(filtered, query.SortDirection, link => link.ExpiresAt ?? DateTimeOffset.MaxValue),
                ShortLinkListSortBy.Destination => ApplyDirection(filtered, query.SortDirection, link => link.OriginalUrl.AbsoluteUri),
                ShortLinkListSortBy.Code => ApplyDirection(filtered, query.SortDirection, link => link.Code),
                ShortLinkListSortBy.Status => ApplyDirection(filtered, query.SortDirection, link => GetStatusRank(link, query.Now)),
                _ => ApplyDirection(filtered, query.SortDirection, link => link.CreatedAt)
            };

            return Task.FromResult(new ShortLinkListPage(
                sorted.Skip(skip).Take(limit).ToList(),
                filtered.Count));
        }

        private static IEnumerable<ShortLink> ApplyDirection<TKey>(
            IEnumerable<ShortLink> shortLinks,
            ShortLinkSortDirection direction,
            Func<ShortLink, TKey> keySelector)
        {
            return direction == ShortLinkSortDirection.Asc
                ? shortLinks.OrderBy(keySelector).ThenBy(link => link.Code, StringComparer.Ordinal)
                : shortLinks.OrderByDescending(keySelector).ThenBy(link => link.Code, StringComparer.Ordinal);
        }

        private static int GetStatusRank(ShortLink shortLink, DateTimeOffset now)
        {
            if (!shortLink.IsActive)
            {
                return 2;
            }

            return shortLink.IsExpired(now) ? 1 : 0;
        }

        public Task<ShortLink?> FindByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            FindByCodeCallCount++;
            links.TryGetValue(code, out var shortLink);
            return Task.FromResult(shortLink);
        }

        public Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default) =>
            Task.FromResult(links.ContainsKey(code));

        public Task AddAsync(ShortLink shortLink, CancellationToken cancellationToken = default)
        {
            links.Add(shortLink.Code, shortLink);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ShortLink shortLink, CancellationToken cancellationToken = default)
        {
            links[shortLink.Code] = shortLink;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string code, CancellationToken cancellationToken = default)
        {
            links.Remove(code);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryShortLinkCache : IShortLinkCache
    {
        private readonly Dictionary<string, ShortLink> links = new(StringComparer.Ordinal);

        public Task<ShortLink?> FindByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            links.TryGetValue(code, out var shortLink);
            return Task.FromResult(shortLink);
        }

        public Task SetAsync(ShortLink shortLink, CancellationToken cancellationToken = default)
        {
            links[shortLink.Code] = shortLink;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string code, CancellationToken cancellationToken = default)
        {
            links.Remove(code);
            return Task.CompletedTask;
        }
    }

    private sealed class SequenceCodeGenerator : IShortCodeGenerator
    {
        private readonly Queue<string> codes;

        public SequenceCodeGenerator(params string[] codes)
        {
            this.codes = new Queue<string>(codes);
        }

        public string Generate(int length = Base62ShortCodeGenerator.DefaultCodeLength) =>
            codes.Count > 0 ? codes.Dequeue() : new string('a', length);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset now;

        public FixedTimeProvider(DateTimeOffset now)
        {
            this.now = now;
        }

        public override DateTimeOffset GetUtcNow() => now;
    }
}
