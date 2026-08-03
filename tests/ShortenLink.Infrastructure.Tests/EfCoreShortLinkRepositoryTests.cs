using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ShortenLink.Core.Abstractions;
using ShortenLink.Core.Domain;
using ShortenLink.Core.Exceptions;
using ShortenLink.Core.Security;
using ShortenLink.Core.Services;
using ShortenLink.Infrastructure.Persistence;
using ShortenLink.Infrastructure.Repositories;
using Xunit;

namespace ShortenLink.Infrastructure.Tests;

public sealed class EfCoreShortLinkRepositoryTests
{
    [Fact]
    public async Task AddAsync_PersistsAndFindsShortLink()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var repository = database.CreateRepository();
        var createdAt = new DateTimeOffset(2026, 7, 11, 9, 30, 0, TimeSpan.Zero);
        var expiresAt = createdAt.AddDays(7);
        var shortLink = new ShortLink(
            "abc1234",
            new Uri("https://example.com/docs"),
            createdAt,
            expiresAt,
            createdByUserId: "user-1",
            createdByDisplayName: "Ada Lovelace",
            createdByUsername: "ada@example.com");

        await repository.AddAsync(shortLink);

        var stored = await repository.FindByCodeAsync("abc1234");

        Assert.NotNull(stored);
        Assert.Equal("abc1234", stored.Code);
        Assert.Equal("https://example.com/docs", stored.OriginalUrl.AbsoluteUri.TrimEnd('/'));
        Assert.Equal(createdAt, stored.CreatedAt);
        Assert.Equal(expiresAt, stored.ExpiresAt);
        Assert.True(stored.IsActive);
        Assert.Equal("user-1", stored.CreatedByUserId);
        Assert.Equal("Ada Lovelace", stored.CreatedByDisplayName);
        Assert.Equal("ada@example.com", stored.CreatedByUsername);
    }

    [Fact]
    public async Task ListPageAsync_AppliesOwnerAndSharedAccessScope()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var repository = database.CreateRepository();
        var now = new DateTimeOffset(2026, 7, 24, 3, 0, 0, TimeSpan.Zero);
        await repository.AddAsync(new ShortLink(
            "owned01", new Uri("https://example.com/owned"), now,
            createdByUserId: "user-1"));
        await repository.AddAsync(new ShortLink(
            "shared1", new Uri("https://example.com/shared"), now.AddMinutes(1),
            createdByUserId: "user-2"));
        await repository.AddAsync(new ShortLink(
            "private", new Uri("https://example.com/private"), now.AddMinutes(2),
            createdByUserId: "user-3"));

        var page = await repository.ListPageAsync(
            0,
            25,
            new ShortLinkListQuery(
                null,
                ShortLinkListStatus.All,
                ShortLinkListSortBy.Created,
                ShortLinkSortDirection.Desc,
                now,
                now.AddDays(7),
                new ShortLinkAccessScope(
                    "user-1",
                    false,
                    new Dictionary<string, ShortLinkShareAccess>
                    {
                        ["shared1"] = ShortLinkShareAccess.View
                    })));

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(new[] { "shared1", "owned01" }, page.Items.Select(item => item.Code));
    }

    [Fact]
    public async Task ExistsByCodeAsync_ReturnsTrueOnlyWhenCodeExists()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var repository = database.CreateRepository();
        await repository.AddAsync(new ShortLink("exists", new Uri("https://example.com"), DateTimeOffset.UtcNow));

        Assert.True(await repository.ExistsByCodeAsync("exists"));
        Assert.False(await repository.ExistsByCodeAsync("missing"));
    }

    [Fact]
    public async Task UpdateAsync_PersistsDeactivatedState()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var repository = database.CreateRepository();
        var shortLink = new ShortLink("active1", new Uri("https://example.com"), DateTimeOffset.UtcNow);
        await repository.AddAsync(shortLink);

        shortLink.Deactivate();
        await repository.UpdateAsync(shortLink);

        var stored = await repository.FindByCodeAsync("active1");

        Assert.NotNull(stored);
        Assert.False(stored.IsActive);
    }

    [Fact]
    public async Task ListRecentAsync_ReturnsNewestLinksFirst()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var repository = database.CreateRepository();
        await repository.AddAsync(new ShortLink(
            "oldest1",
            new Uri("https://example.com/old"),
            new DateTimeOffset(2026, 7, 11, 9, 0, 0, TimeSpan.Zero)));
        await repository.AddAsync(new ShortLink(
            "newest1",
            new Uri("https://example.com/new"),
            new DateTimeOffset(2026, 7, 12, 9, 0, 0, TimeSpan.Zero)));

        var links = await repository.ListRecentAsync(10);

        Assert.Collection(
            links,
            link => Assert.Equal("newest1", link.Code),
            link => Assert.Equal("oldest1", link.Code));
    }

    [Fact]
    public async Task ListExpirationCandidatesAsync_UsesStableCursorAndTenantScope()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var repository = database.CreateRepository();
        var baseTime = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
        await repository.AddAsync(new ShortLink(
            "expire-a1", new Uri("https://example.com/a1"), baseTime.AddDays(-3),
            baseTime.AddDays(-2), tenantId: "tenant-a"));
        await repository.AddAsync(new ShortLink(
            "expire-a2", new Uri("https://example.com/a2"), baseTime.AddDays(-2),
            baseTime.AddDays(-1), tenantId: "tenant-a"));
        await repository.AddAsync(new ShortLink(
            "expire-b1", new Uri("https://example.com/b1"), baseTime.AddDays(-3),
            baseTime.AddDays(-2), tenantId: "tenant-b"));

        var expirationRepository = (IShortLinkExpirationRepository)repository;
        var first = await expirationRepository.ListExpirationCandidatesAsync(
            "tenant-a", null, null, 1);
        var second = await expirationRepository.ListExpirationCandidatesAsync(
            "tenant-a", first[0].ExpiresAt, first[0].Code, 10);

        Assert.Equal("expire-a1", Assert.Single(first).Code);
        Assert.Equal("expire-a2", Assert.Single(second).Code);
    }

    [Fact]
    public async Task DeleteAsync_RemovesShortLink()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var repository = database.CreateRepository();
        await repository.AddAsync(new ShortLink("delete1", new Uri("https://example.com/delete"), DateTimeOffset.UtcNow));

        await repository.DeleteAsync("delete1");

        Assert.Null(await repository.FindByCodeAsync("delete1"));
    }

    [Fact]
    public async Task AddAsync_EnforcesUniqueCode()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var repository = database.CreateRepository();
        await repository.AddAsync(new ShortLink("dupe", new Uri("https://example.com/one"), DateTimeOffset.UtcNow));
        var secondRepository = database.CreateRepository();

        await Assert.ThrowsAsync<ShortLinkCodeConflictException>(() =>
            secondRepository.AddAsync(new ShortLink("dupe", new Uri("https://example.com/two"), DateTimeOffset.UtcNow)));

        await secondRepository.AddAsync(new ShortLink("fresh", new Uri("https://example.com/fresh"), DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task AddAsync_EnforcesUniqueIdempotencyKeyAndFindsOriginalLink()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var repository = database.CreateRepository();
        var first = new ShortLink(
            "idem001",
            new Uri("https://example.com/one"),
            DateTimeOffset.UtcNow,
            idempotencyKey: "create-123");
        await repository.AddAsync(first);
        var secondRepository = database.CreateRepository();

        var found = await secondRepository.FindByIdempotencyKeyAsync("create-123");

        Assert.Equal(first.Code, found?.Code);
        await Assert.ThrowsAsync<ShortLinkIdempotencyConflictException>(() =>
            secondRepository.AddAsync(new ShortLink(
                "idem002",
                new Uri("https://example.com/two"),
                DateTimeOffset.UtcNow,
                idempotencyKey: "create-123")));
    }

    [Fact]
    public async Task TenantPartition_ScopesReadsAndIdempotencyKeys()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var repository = database.CreateRepository();
        var now = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        var tenantA = new ShortLink(
            "tenanta1",
            new Uri("https://example.com/tenant-a"),
            now,
            createdByUserId: "user-1",
            idempotencyKey: "shared-key",
            tenantId: "tenant-a");
        var tenantB = new ShortLink(
            "tenantb1",
            new Uri("https://example.com/tenant-b"),
            now.AddMinutes(1),
            createdByUserId: "user-1",
            idempotencyKey: "shared-key",
            tenantId: "tenant-b");

        await repository.AddAsync(tenantA);
        await repository.AddAsync(tenantB);
        var tenantAList = await repository.ListAccessibleRecentAsync(
            10,
            null,
            null,
            new ShortLinkAccessScope(
                "user-1",
                IsAdmin: true,
                new Dictionary<string, ShortLinkShareAccess>(),
                "tenant-a"));
        var foundA = await repository.FindByTenantIdempotencyKeyAsync("tenant-a", "shared-key");
        var foundB = await repository.FindByTenantIdempotencyKeyAsync("tenant-b", "shared-key");

        Assert.Equal(tenantA.Code, Assert.Single(tenantAList).Code);
        Assert.Equal(tenantA.Code, foundA?.Code);
        Assert.Equal(tenantB.Code, foundB?.Code);
        await Assert.ThrowsAsync<ShortLinkIdempotencyConflictException>(() =>
            repository.AddAsync(new ShortLink(
                "tenanta2",
                new Uri("https://example.com/tenant-a/conflict"),
                now.AddMinutes(2),
                idempotencyKey: "shared-key",
                tenantId: "tenant-a")));
    }

    [Fact]
    public async Task Schema_HasExpectedIndexes()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();

        var indexes = await database.GetIndexNamesAsync();

        Assert.Contains("IX_short_links_Code", indexes);
        Assert.Contains("IX_short_links_CreatedAt", indexes);
        Assert.Contains("IX_short_links_ExpiresAt", indexes);
        Assert.Contains("IX_short_links_IsActive", indexes);
        Assert.Contains("IX_short_links_TenantId_CreatedAt_Code", indexes);
        Assert.Contains("IX_short_links_TenantId_ExpiresAt_Code", indexes);
        Assert.Contains("IX_short_links_TenantId_IdempotencyKey", indexes);
        Assert.DoesNotContain("IX_short_links_IdempotencyKey", indexes);
    }

    [Fact]
    public void Model_WithPostgresProvider_KeepsExpectedIndexes()
    {
        var options = new DbContextOptionsBuilder<ShortLinkDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=shorten_link_tests;Username=postgres;Password=postgres")
            .Options;

        using var context = new ShortLinkDbContext(options);
        var entityType = context.Model.FindEntityType(typeof(ShortLinkPersistenceEntity));

        Assert.NotNull(entityType);

        var indexNames = entityType!
            .GetIndexes()
            .Select(index => index.GetDatabaseName())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("IX_short_links_Code", indexNames);
        Assert.Contains("IX_short_links_CreatedAt", indexNames);
        Assert.Contains("IX_short_links_ExpiresAt", indexNames);
        Assert.Contains("IX_short_links_IsActive", indexNames);
        Assert.Contains("IX_short_links_TenantId_CreatedAt_Code", indexNames);
        Assert.Contains("IX_short_links_TenantId_ExpiresAt_Code", indexNames);
        Assert.Contains("IX_short_links_TenantId_IdempotencyKey", indexNames);
    }

    [Fact]
    public async Task CompatibilitySchema_AddsTenantPartitionAndReplacesLegacyIndex()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE "short_links" (
                    "Id" TEXT NOT NULL PRIMARY KEY,
                    "IdempotencyKey" TEXT NULL
                );
                CREATE UNIQUE INDEX "IX_short_links_IdempotencyKey"
                    ON "short_links" ("IdempotencyKey");
                """;
            await command.ExecuteNonQueryAsync();
        }

        var options = new DbContextOptionsBuilder<ShortLinkDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new ShortLinkDbContext(options);

        await ShortLinkDatabaseSchema.EnsureIdempotencySchemaAsync(context);

        var columns = new HashSet<string>(StringComparer.Ordinal);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA table_info('short_links');";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(1));
            }
        }

        var indexes = new HashSet<string>(StringComparer.Ordinal);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'index' AND tbl_name = 'short_links';";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                indexes.Add(reader.GetString(0));
            }
        }

        Assert.Contains("TenantId", columns);
        Assert.Contains("IX_short_links_TenantId_IdempotencyKey", indexes);
        Assert.DoesNotContain("IX_short_links_IdempotencyKey", indexes);
    }

    private sealed class SqliteTestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private SqliteTestDatabase(SqliteConnection connection)
        {
            this.connection = connection;
        }

        public static async Task<SqliteTestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var database = new SqliteTestDatabase(connection);
            await using var context = database.CreateContext();
            await context.Database.EnsureCreatedAsync();

            return database;
        }

        public ShortLinkDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ShortLinkDbContext>()
                .UseSqlite(connection)
                .Options;

            return new ShortLinkDbContext(options);
        }

        public EfCoreShortLinkRepository CreateRepository() =>
            new(CreateContext());

        public async Task<HashSet<string>> GetIndexNamesAsync()
        {
            var indexes = new HashSet<string>(StringComparer.Ordinal);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'index' AND tbl_name = 'short_links'";

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                indexes.Add(reader.GetString(0));
            }

            return indexes;
        }

        public async ValueTask DisposeAsync()
        {
            await connection.DisposeAsync();
        }
    }
}
