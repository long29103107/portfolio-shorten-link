using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ShortenLink.Core.Domain;
using ShortenLink.Infrastructure.Persistence;
using ShortenLink.Infrastructure.Persistence.Entities;
using ShortenLink.Infrastructure.Repositories;
using Xunit;

namespace ShortenLink.Infrastructure.Tests;

public sealed class EfCoreShortLinkClickRepositoryTests
{
    [Fact]
    public async Task AddAsync_PersistsShortLinkClick()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var repository = database.CreateRepository();
        var clickedAtUtc = new DateTimeOffset(2026, 7, 12, 8, 30, 0, TimeSpan.Zero);

        await repository.AddAsync(new ShortLinkClick(
            "jump01",
            clickedAtUtc,
            "127.0.0.1",
            "integration-test-agent",
            "https://example.com/from"));

        var stored = await database.GetClicksAsync();
        var click = Assert.Single(stored);
        Assert.Equal("jump01", click.ShortCode);
        Assert.Equal(clickedAtUtc, click.ClickedAtUtc);
        Assert.Equal("127.0.0.1", click.RemoteIpAddress);
        Assert.Equal("integration-test-agent", click.UserAgent);
        Assert.Equal("https://example.com/from", click.Referrer);
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsClickCountAndLastClickedAt()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var repository = database.CreateRepository();
        var firstClick = new DateTimeOffset(2026, 7, 12, 8, 30, 0, TimeSpan.Zero);
        var secondClick = firstClick.AddHours(2);

        await repository.AddAsync(new ShortLinkClick("sum001", firstClick, null, null, null));
        await repository.AddAsync(new ShortLinkClick("sum001", secondClick, null, null, null));
        await repository.AddAsync(new ShortLinkClick("other1", secondClick.AddHours(1), null, null, null));

        var summary = await repository.GetSummaryAsync("sum001");

        Assert.Equal("sum001", summary.ShortCode);
        Assert.Equal(2, summary.ClickCount);
        Assert.Equal(secondClick, summary.LastClickedAtUtc);
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsZeroForNoClicks()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var repository = database.CreateRepository();

        var summary = await repository.GetSummaryAsync("empty1");

        Assert.Equal("empty1", summary.ShortCode);
        Assert.Equal(0, summary.ClickCount);
        Assert.Null(summary.LastClickedAtUtc);
    }

    [Fact]
    public async Task ListRecentAsync_ReturnsNewestClicksFirstWithSafeLimit()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var repository = database.CreateRepository();
        var baseTime = new DateTimeOffset(2026, 7, 12, 8, 30, 0, TimeSpan.Zero);

        await repository.AddAsync(new ShortLinkClick("recent1", baseTime, "127.0.0.1", "old", null));
        await repository.AddAsync(new ShortLinkClick("recent1", baseTime.AddMinutes(5), "127.0.0.2", "new", null));
        await repository.AddAsync(new ShortLinkClick("recent1", baseTime.AddMinutes(3), "127.0.0.3", "middle", null));
        await repository.AddAsync(new ShortLinkClick("other2", baseTime.AddMinutes(10), "127.0.0.4", "other", null));

        var clicks = await repository.ListRecentAsync("recent1", 2);

        Assert.Collection(
            clicks,
            click =>
            {
                Assert.Equal(baseTime.AddMinutes(5), click.ClickedAtUtc);
                Assert.Equal("new", click.UserAgent);
            },
            click =>
            {
                Assert.Equal(baseTime.AddMinutes(3), click.ClickedAtUtc);
                Assert.Equal("middle", click.UserAgent);
            });
    }

    [Fact]
    public async Task Schema_HasExpectedIndexes()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();

        var indexes = await database.GetIndexNamesAsync();

        Assert.Contains("IX_short_link_clicks_ShortCode", indexes);
        Assert.Contains("IX_short_link_clicks_ClickedAtUtc", indexes);
        Assert.Contains("IX_short_link_clicks_ShortCode_ClickedAtUtc", indexes);
        Assert.Contains("IX_short_link_clicks_TenantId_ShortCode", indexes);
        Assert.Contains("IX_short_link_clicks_TenantId_ShortCode_ClickedAtUtc", indexes);
    }

    [Fact]
    public async Task TenantQueries_IsolateClicksWithTheSameCode()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var repository = database.CreateRepository();
        var now = new DateTimeOffset(2026, 7, 12, 8, 30, 0, TimeSpan.Zero);

        await repository.AddAsync(new ShortLinkClick(
            "tenant01", now, null, "tenant-a", null, tenantId: "tenant-a"));
        await repository.AddAsync(new ShortLinkClick(
            "tenant01", now.AddMinutes(1), null, "tenant-b", null, tenantId: "tenant-b"));

        var tenantRepository = (ITenantAwareShortLinkClickRepository)repository;
        var summary = await tenantRepository.GetSummaryAsync("tenant01", "tenant-a");
        var recent = await tenantRepository.ListRecentAsync("tenant01", "tenant-a", 10);

        Assert.Equal(1, summary.ClickCount);
        Assert.Equal("tenant-a", Assert.Single(recent).TenantId);
    }

    [Fact]
    public void Model_WithPostgresProvider_KeepsExpectedIndexes()
    {
        var options = new DbContextOptionsBuilder<ShortLinkDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=shorten_link_tests;Username=postgres;Password=postgres")
            .Options;

        using var context = new ShortLinkDbContext(options);
        var entityType = context.Model.FindEntityType(typeof(ShortLinkClickPersistenceEntity));

        Assert.NotNull(entityType);

        var indexNames = entityType!
            .GetIndexes()
            .Select(index => index.GetDatabaseName())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("IX_short_link_clicks_ShortCode", indexNames);
        Assert.Contains("IX_short_link_clicks_ClickedAtUtc", indexNames);
        Assert.Contains("IX_short_link_clicks_ShortCode_ClickedAtUtc", indexNames);
        Assert.Contains("IX_short_link_clicks_TenantId_ShortCode", indexNames);
        Assert.Contains("IX_short_link_clicks_TenantId_ShortCode_ClickedAtUtc", indexNames);
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

        public EfCoreShortLinkClickRepository CreateRepository() =>
            new(CreateContext());

        public async Task<List<ShortLinkClickPersistenceEntity>> GetClicksAsync()
        {
            await using var context = CreateContext();
            return await context.ShortLinkClicks
                .AsNoTracking()
                .OrderBy(click => click.Id)
                .ToListAsync();
        }

        public async Task<HashSet<string>> GetIndexNamesAsync()
        {
            var indexes = new HashSet<string>(StringComparer.Ordinal);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'index' AND tbl_name = 'short_link_clicks'";

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
