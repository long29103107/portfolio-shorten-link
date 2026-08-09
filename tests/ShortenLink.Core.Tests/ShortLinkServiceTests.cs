using ShortenLink.Core.Domain;
using ShortenLink.Core.Generation;
using ShortenLink.Core.Exceptions;
using ShortenLink.Core.Abstractions;
using ShortenLink.Core.Events;
using ShortenLink.Core;
using ShortenLink.Application.Services;
using ShortenLink.Core.Services;
using ShortenLink.Core.Security;
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
    public async Task CreateAsync_StopsAfterConfiguredCodeGenerationAttempts()
    {
        var now = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        var repository = new InMemoryShortLinkRepository();
        await repository.AddAsync(new ShortLink("taken01", new Uri("https://example.com"), now));
        var service = new ShortLinkService(
            repository,
            new SequenceCodeGenerator("taken01", "taken01", "fresh01"),
            timeProvider: new FixedTimeProvider(now),
            maxCodeGenerationAttempts: 2);

        var result = await service.CreateAsync(
            new CreateShortLinkRequest("https://openai.com", now.AddDays(1)));

        Assert.False(result.Succeeded);
        Assert.Equal(ShortLinkErrorCodes.UnableToGenerateCode, result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_RetriesWhenRepositoryReportsCodeConflict()
    {
        var now = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        var repository = new InMemoryShortLinkRepository { RemainingCodeConflicts = 1 };
        var service = new ShortLinkService(
            repository,
            new SequenceCodeGenerator("first01", "fresh01"),
            timeProvider: new FixedTimeProvider(now),
            maxCodeGenerationAttempts: 2);

        var result = await service.CreateAsync(
            new CreateShortLinkRequest("https://openai.com", now.AddDays(1)));

        Assert.True(result.Succeeded);
        Assert.Equal("fresh01", result.ShortLink?.Code);
        Assert.Equal(2, repository.AddAttemptCount);
    }

    [Fact]
    public async Task CreateAsync_ReplaysEquivalentIdempotencyKeyWithoutCreatingAnotherLink()
    {
        var now = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        var repository = new InMemoryShortLinkRepository();
        var service = CreateService(
            repository,
            new SequenceCodeGenerator("idem001", "idem002"),
            timeProvider: new FixedTimeProvider(now));
        var request = new CreateShortLinkRequest(
            "https://example.com/retry",
            now.AddDays(1),
            "user-1",
            IdempotencyKey: "create-123");

        var first = await service.CreateAsync(request);
        var replay = await service.CreateAsync(request);

        Assert.True(first.Succeeded);
        Assert.False(first.Replayed);
        Assert.True(replay.Succeeded);
        Assert.True(replay.Replayed);
        Assert.Equal(first.ShortLink?.Code, replay.ShortLink?.Code);
        Assert.Equal(1, repository.Count);
        Assert.Equal(1, repository.AddAttemptCount);
    }

    [Fact]
    public async Task CreateAsync_RejectsIdempotencyKeyReuseForDifferentRequest()
    {
        var now = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        var repository = new InMemoryShortLinkRepository();
        var service = CreateService(
            repository,
            new SequenceCodeGenerator("idem001", "idem002"),
            timeProvider: new FixedTimeProvider(now));

        var first = await service.CreateAsync(new CreateShortLinkRequest(
            "https://example.com/first",
            now.AddDays(1),
            "user-1",
            IdempotencyKey: "create-123"));
        var conflict = await service.CreateAsync(new CreateShortLinkRequest(
            "https://example.com/second",
            now.AddDays(1),
            "user-1",
            IdempotencyKey: "create-123"));

        Assert.True(first.Succeeded);
        Assert.False(conflict.Succeeded);
        Assert.Equal(ShortLinkErrorCodes.IdempotencyConflict, conflict.ErrorCode);
        Assert.Equal(1, repository.Count);
    }

    [Fact]
    public async Task CreateAsync_ScopesIdempotencyAndAccessibleReadsByTenant()
    {
        var now = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        var repository = new InMemoryShortLinkRepository();
        var service = CreateService(
            repository,
            new SequenceCodeGenerator("tenant1", "tenant2"),
            timeProvider: new FixedTimeProvider(now));
        var tenantARequest = new CreateShortLinkRequest(
            "https://example.com/tenant-a",
            now.AddDays(1),
            "user-1",
            IdempotencyKey: "shared-key",
            TenantId: " tenant-a ");
        var tenantBRequest = new CreateShortLinkRequest(
            "https://example.com/tenant-b",
            now.AddDays(1),
            "user-1",
            IdempotencyKey: "shared-key",
            TenantId: "tenant-b");

        var tenantA = await service.CreateAsync(tenantARequest);
        var tenantB = await service.CreateAsync(tenantBRequest);
        var tenantAReplay = await service.CreateAsync(tenantARequest);
        var tenantAList = await service.ListAccessibleRecentAsync(
            10,
            null,
            null,
            new ShortLinkAccessScope(
                "user-1",
                IsAdmin: true,
                new Dictionary<string, ShortLinkShareAccess>(),
                "tenant-a"));

        Assert.True(tenantA.Succeeded);
        Assert.True(tenantB.Succeeded);
        Assert.True(tenantAReplay.Replayed);
        Assert.Equal("tenant-a", tenantA.ShortLink?.TenantId);
        Assert.Equal("tenant-b", tenantB.ShortLink?.TenantId);
        Assert.NotEqual(tenantA.ShortLink?.Code, tenantB.ShortLink?.Code);
        Assert.Equal(2, repository.Count);
        Assert.Equal(tenantA.ShortLink?.Code, Assert.Single(tenantAList).Code);
    }

    [Fact]
    public async Task CreateAsync_FailsClosedWhenProviderDoesNotSupportTenantPartitions()
    {
        var service = new ShortLinkService(
            new NonTenantShortLinkRepository(),
            new SequenceCodeGenerator("tenant1"));

        var result = await service.CreateAsync(new CreateShortLinkRequest(
            "https://example.com/tenant",
            DateTimeOffset.UtcNow.AddDays(1),
            TenantId: "tenant-a"));

        Assert.False(result.Succeeded);
        Assert.Equal(ShortLinkErrorCodes.TenantNotSupported, result.ErrorCode);
    }

    [Fact]
    public async Task ResolveAsync_TenantPartitionDoesNotAcceptAnotherTenantsCacheOrRepositoryRecord()
    {
        var repository = new InMemoryShortLinkRepository();
        var cache = new InMemoryShortLinkCache();
        var link = new ShortLink(
            "shared01",
            new Uri("https://example.com/tenant-a"),
            DateTimeOffset.UtcNow,
            tenantId: "tenant-a");
        await repository.AddAsync(link);
        await cache.SetAsync(link);
        var service = CreateService(repository, cache: cache);

        var tenantB = await service.ResolveAsync(
            "shared01",
            CancellationToken.None,
            "tenant-b");
        var tenantA = await service.ResolveAsync(
            "shared01",
            CancellationToken.None,
            "tenant-a");

        Assert.False(tenantB.Succeeded);
        Assert.Equal(ShortLinkErrorCodes.NotFound, tenantB.ErrorCode);
        Assert.True(tenantA.Succeeded);
        Assert.Equal("tenant-a", tenantA.ShortLink?.TenantId);
    }

    [Fact]
    public async Task CreateAsync_RejectsOversizedIdempotencyKey()
    {
        var service = CreateService();

        var result = await service.CreateAsync(new CreateShortLinkRequest(
            "https://example.com",
            DateTimeOffset.UtcNow.AddDays(1),
            IdempotencyKey: new string('x', ShortLinkIdempotencyKey.MaxLength + 1)));

        Assert.False(result.Succeeded);
        Assert.Equal(ShortLinkErrorCodes.InvalidIdempotencyKey, result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_DoesNotRetryUnexpectedPersistenceFailures()
    {
        var now = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        var repository = new InMemoryShortLinkRepository { ThrowUnexpectedAddFailure = true };
        var service = CreateService(
            repository,
            new SequenceCodeGenerator("first01"),
            timeProvider: new FixedTimeProvider(now));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new CreateShortLinkRequest("https://openai.com", now.AddDays(1))));

        Assert.Equal(1, repository.AddAttemptCount);
    }

    [Fact]
    public async Task LifecycleOperations_PublishVersionedSafeEvents()
    {
        var now = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        var repository = new InMemoryShortLinkRepository();
        var sink = new CapturingEventSink();
        var service = CreateService(
            repository,
            new SequenceCodeGenerator("event01"),
            timeProvider: new FixedTimeProvider(now),
            eventSink: sink);

        var created = await service.CreateAsync(
            new CreateShortLinkRequest(
                "https://example.com/private-destination",
                now.AddDays(1),
                "user-1"));
        await service.UpdateAsync(
            "event01",
            new UpdateShortLinkRequest("https://example.com/updated", now.AddDays(2)));
        await service.DeactivateAsync("event01");
        await service.ActivateAsync("event01");
        var redirected = await service.ResolveAsync("event01");
        await service.DeleteAsync("event01");

        Assert.True(created.Succeeded);
        Assert.True(redirected.Succeeded);
        Assert.Equal(
            new[]
            {
                ShortLinkEventTypes.Created,
                ShortLinkEventTypes.Updated,
                ShortLinkEventTypes.Deactivated,
                ShortLinkEventTypes.Activated,
                ShortLinkEventTypes.Redirected,
                ShortLinkEventTypes.Deleted
            },
            sink.Events.Select(item => item.EventType));
        Assert.All(sink.Events, item => Assert.Equal(ShortLinkLifecycleEvent.CurrentVersion, item.Version));
        Assert.DoesNotContain(sink.Events, item => item.Code == "user-1");
        Assert.DoesNotContain(
            sink.SerializedEvents,
            serialized => serialized.Contains("private-destination", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EventSinkFailures_DoNotFailSuccessfulOperations()
    {
        var now = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        var service = CreateService(
            timeProvider: new FixedTimeProvider(now),
            eventSink: new ThrowingEventSink());

        var result = await service.CreateAsync(
            new CreateShortLinkRequest("https://example.com", now.AddDays(1)));

        Assert.True(result.Succeeded);
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
    public async Task CreateAsync_RejectsActivationAtOrAfterExpiration()
    {
        var now = new DateTimeOffset(2026, 7, 11, 0, 0, 0, TimeSpan.Zero);
        var service = CreateService(timeProvider: new FixedTimeProvider(now));

        var result = await service.CreateAsync(new CreateShortLinkRequest(
            "https://openai.com",
            now.AddHours(2),
            ActiveFrom: now.AddHours(2)));

        Assert.False(result.Succeeded);
        Assert.Equal(ShortLinkErrorCodes.InvalidActivationWindow, result.ErrorCode);
    }

    [Fact]
    public async Task ResolveAsync_RejectsScheduledLinkUntilActivationTime()
    {
        var now = new DateTimeOffset(2026, 7, 11, 0, 0, 0, TimeSpan.Zero);
        var repository = new InMemoryShortLinkRepository();
        await repository.AddAsync(new ShortLink(
            "scheduled",
            new Uri("https://example.com/scheduled"),
            now,
            now.AddDays(1),
            activeFrom: now.AddHours(1)));

        var beforeActivation = await CreateService(
            repository,
            timeProvider: new FixedTimeProvider(now)).ResolveAsync("scheduled");
        var afterActivation = await CreateService(
            repository,
            timeProvider: new FixedTimeProvider(now.AddHours(1))).ResolveAsync("scheduled");

        Assert.False(beforeActivation.Succeeded);
        Assert.Equal(ShortLinkErrorCodes.Scheduled, beforeActivation.ErrorCode);
        Assert.True(afterActivation.Succeeded);
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
        TimeProvider? timeProvider = null,
        IShortLinkEventSink? eventSink = null)
    {
        return new ShortLinkService(
            repository ?? new InMemoryShortLinkRepository(),
            generator ?? new SequenceCodeGenerator("abc1234"),
            cache,
            timeProvider,
            eventSink: eventSink);
    }

    private sealed class InMemoryShortLinkRepository :
        IShortLinkRepository,
        IShortLinkIdempotencyRepository,
        IShortLinkTenantRepository
    {
        private readonly Dictionary<string, ShortLink> links = new(StringComparer.Ordinal);

        public int FindByCodeCallCount { get; private set; }

        public int AddAttemptCount { get; private set; }

        public int Count => links.Count;

        public int RemainingCodeConflicts { get; set; }

        public bool ThrowUnexpectedAddFailure { get; set; }

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

        public Task<IReadOnlyList<ShortLink>> ListAccessibleRecentAsync(
            int limit,
            DateTimeOffset? beforeCreatedAt,
            string? beforeCode,
            ShortLinkAccessScope accessScope,
            CancellationToken cancellationToken = default)
        {
            var result = links.Values
                .Where(link => string.Equals(
                    link.TenantId,
                    ShortLinkTenantId.Normalize(accessScope.TenantId),
                    StringComparison.Ordinal))
                .Where(link => accessScope.IsAdmin
                    || string.Equals(link.CreatedByUserId, accessScope.UserId, StringComparison.Ordinal)
                    || accessScope.SharedAccess.ContainsKey(link.Code))
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

        public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(links.Count);

        public Task<ShortLinkListPage> ListPageAsync(
            int skip,
            int limit,
            ShortLinkListQuery query,
            CancellationToken cancellationToken = default)
        {
            var filtered = links.Values
                .Where(link => query.AccessScope is null
                    ? link.TenantId is null
                    : string.Equals(
                        link.TenantId,
                        ShortLinkTenantId.Normalize(query.AccessScope.TenantId),
                        StringComparison.Ordinal))
                .Where(CreateFilterPredicate(query.FilterExpression))
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

        private static Func<ShortLink, bool> CreateFilterPredicate(string? filterExpression)
        {
            if (string.IsNullOrWhiteSpace(filterExpression))
            {
                return static _ => true;
            }

            var predicate = ShortenLink.Core.Querying.FilterExpressionParser.Parse<ShortLinkFilterRow>(
                filterExpression,
                ShortLinkFilterRow.AllowedProperties).Compile();
            return link => predicate(new ShortLinkFilterRow
            {
                Code = link.Code,
                OriginalUrl = link.OriginalUrl.AbsoluteUri,
                ExpiresAt = link.ExpiresAt,
                IsActive = link.IsActive,
                CreatedAt = link.CreatedAt,
                CreatedByUserId = link.CreatedByUserId
            });
        }

        private sealed class ShortLinkFilterRow
        {
            public static readonly string[] AllowedProperties =
            [nameof(Code), nameof(OriginalUrl), nameof(ExpiresAt), nameof(IsActive), nameof(CreatedAt), nameof(CreatedByUserId)];

            public string Code { get; init; } = string.Empty;
            public string OriginalUrl { get; init; } = string.Empty;
            public DateTimeOffset? ExpiresAt { get; init; }
            public bool IsActive { get; init; }
            public DateTimeOffset CreatedAt { get; init; }
            public string? CreatedByUserId { get; init; }
        }

        public Task<ShortLink?> FindByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            FindByCodeCallCount++;
            links.TryGetValue(code, out var shortLink);
            return Task.FromResult(shortLink);
        }

        public Task<ShortLink?> FindByIdempotencyKeyAsync(
            string idempotencyKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(links.Values.FirstOrDefault(link =>
                link.TenantId is null
                && string.Equals(link.IdempotencyKey, idempotencyKey, StringComparison.Ordinal)));

        public Task<ShortLink?> FindByTenantIdempotencyKeyAsync(
            string tenantId,
            string idempotencyKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(links.Values.FirstOrDefault(link =>
                string.Equals(link.TenantId, tenantId, StringComparison.Ordinal)
                && string.Equals(link.IdempotencyKey, idempotencyKey, StringComparison.Ordinal)));

        public Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default) =>
            Task.FromResult(links.ContainsKey(code));

        public Task AddAsync(ShortLink shortLink, CancellationToken cancellationToken = default)
        {
            AddAttemptCount++;
            if (ThrowUnexpectedAddFailure)
            {
                throw new InvalidOperationException("persistence unavailable");
            }

            if (RemainingCodeConflicts > 0)
            {
                RemainingCodeConflicts--;
                throw new ShortLinkCodeConflictException(shortLink.Code);
            }

            if (shortLink.IdempotencyKey is not null
                && links.Values.Any(link => string.Equals(
                    link.IdempotencyKey,
                    shortLink.IdempotencyKey,
                    StringComparison.Ordinal)
                    && string.Equals(link.TenantId, shortLink.TenantId, StringComparison.Ordinal)))
            {
                throw new ShortLinkIdempotencyConflictException();
            }

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

    private sealed class NonTenantShortLinkRepository : IShortLinkRepository
    {
        private readonly InMemoryShortLinkRepository inner = new();

        public Task<IReadOnlyList<ShortLink>> ListRecentAsync(
            int limit,
            DateTimeOffset? beforeCreatedAt = null,
            string? beforeCode = null,
            CancellationToken cancellationToken = default) =>
            inner.ListRecentAsync(limit, beforeCreatedAt, beforeCode, cancellationToken);

        public Task<IReadOnlyList<ShortLink>> ListRecentPageAsync(
            int skip,
            int limit,
            CancellationToken cancellationToken = default) =>
            inner.ListRecentPageAsync(skip, limit, cancellationToken);

        public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
            inner.CountAsync(cancellationToken);

        public Task<ShortLinkListPage> ListPageAsync(
            int skip,
            int limit,
            ShortLinkListQuery query,
            CancellationToken cancellationToken = default) =>
            inner.ListPageAsync(skip, limit, query, cancellationToken);

        public Task<ShortLink?> FindByCodeAsync(
            string code,
            CancellationToken cancellationToken = default) =>
            inner.FindByCodeAsync(code, cancellationToken);

        public Task<bool> ExistsByCodeAsync(
            string code,
            CancellationToken cancellationToken = default) =>
            inner.ExistsByCodeAsync(code, cancellationToken);

        public Task AddAsync(ShortLink shortLink, CancellationToken cancellationToken = default) =>
            inner.AddAsync(shortLink, cancellationToken);

        public Task UpdateAsync(ShortLink shortLink, CancellationToken cancellationToken = default) =>
            inner.UpdateAsync(shortLink, cancellationToken);

        public Task DeleteAsync(string code, CancellationToken cancellationToken = default) =>
            inner.DeleteAsync(code, cancellationToken);
    }

    private sealed class InMemoryShortLinkCache : IShortLinkCache, ITenantAwareShortLinkCache
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
            if (shortLink.TenantId is not null)
            {
                links[$"{shortLink.TenantId}:{shortLink.Code}"] = shortLink;
            }
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string code, CancellationToken cancellationToken = default)
        {
            links.Remove(code);
            return Task.CompletedTask;
        }

        public Task<ShortLink?> FindByCodeAsync(
            string code,
            string tenantId,
            CancellationToken cancellationToken = default)
        {
            links.TryGetValue($"{tenantId}:{code}", out var tenantLink);
            return Task.FromResult(tenantLink);
        }

        public Task RemoveAsync(
            string code,
            string tenantId,
            CancellationToken cancellationToken = default)
        {
            links.Remove($"{tenantId}:{code}");
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingEventSink : IShortLinkEventSink
    {
        public List<ShortLinkLifecycleEvent> Events { get; } = new();

        public List<string> SerializedEvents { get; } = new();

        public bool TryPublish(ShortLinkLifecycleEvent @event, CancellationToken cancellationToken = default)
        {
            Events.Add(@event);
            SerializedEvents.Add(System.Text.Json.JsonSerializer.Serialize(@event));
            return true;
        }
    }

    private sealed class ThrowingEventSink : IShortLinkEventSink
    {
        public bool TryPublish(ShortLinkLifecycleEvent @event, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("sink unavailable");
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
