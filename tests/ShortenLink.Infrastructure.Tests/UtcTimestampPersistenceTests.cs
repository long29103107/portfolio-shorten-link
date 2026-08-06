using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ShortenLink.Auditing;
using ShortenLink.Core.Contracts.Queries;
using ShortenLink.Core.Domain;
using ShortenLink.Infrastructure.Persistence;
using ShortenLink.Infrastructure.Persistence.Entities;
using ShortenLink.Infrastructure.Repositories;
using Xunit;

namespace ShortenLink.Infrastructure.Tests;

public sealed class UtcTimestampPersistenceTests
{
    [Fact]
    public async Task Model_StoresDateTimeOffsetPropertiesAsUtcDateTimeValues()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ShortLinkDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var dbContext = new ShortLinkDbContext(options);

        var entityType = dbContext.Model.FindEntityType(typeof(ShortLinkPersistenceEntity));
        var createdAtConverter = entityType?
            .FindProperty(nameof(ShortLinkPersistenceEntity.CreatedAt))?
            .GetValueConverter();
        var expiresAtConverter = entityType?
            .FindProperty(nameof(ShortLinkPersistenceEntity.ExpiresAt))?
            .GetValueConverter();

        Assert.Equal(typeof(DateTime), createdAtConverter?.ProviderClrType);
        Assert.Equal(typeof(DateTime), expiresAtConverter?.ProviderClrType);
    }

    [Fact]
    public async Task CompatibilityUpgrade_NormalizesLegacyOffsetValuesToUtc()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ShortLinkDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var dbContext = new ShortLinkDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        await dbContext.Database.ExecuteSqlRawAsync("""
            INSERT INTO "short_links"
                ("Id", "CreatedAt", "Code", "OriginalUrl", "ExpiresAt", "IsActive", "TenantId", "SharingMode")
            VALUES
                ('00000000-0000-0000-0000-000000000001',
                 '2026-08-04 12:00:00+07:00',
                 'legacy1',
                 'https://example.com/legacy',
                 '2026-08-04 13:00:00+07:00',
                 1,
                 '',
                 1);
            """);

        await ShortLinkDatabaseSchema.EnsureUtcTimestampSchemaAsync(dbContext);
        var stored = await new EfCoreShortLinkRepository(dbContext)
            .FindByCodeAsync("legacy1");

        Assert.NotNull(stored);
        Assert.Equal(
            new DateTimeOffset(2026, 8, 4, 5, 0, 0, TimeSpan.Zero),
            stored.CreatedAt);
        Assert.Equal(
            new DateTimeOffset(2026, 8, 4, 6, 0, 0, TimeSpan.Zero),
            stored.ExpiresAt);
    }

    [Fact]
    public async Task ListRecentAsync_UsesDirectTimestampPredicateAndLeanProjection()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var interceptor = new CommandCaptureInterceptor();
        var options = new DbContextOptionsBuilder<ShortLinkDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(interceptor)
            .Options;
        await using var dbContext = new ShortLinkDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        var repository = new EfCoreShortLinkRepository(dbContext);
        var createdAt = new DateTimeOffset(2026, 8, 4, 5, 0, 0, TimeSpan.Zero);
        await repository.AddAsync(new ShortLink(
            "project1",
            new Uri("https://example.com/projected"),
            createdAt,
            idempotencyKey: "not-needed-by-list"));
        await using (var rawTimestampCommand = connection.CreateCommand())
        {
            rawTimestampCommand.CommandText =
                "SELECT \"CreatedAt\" FROM \"short_links\" WHERE \"Code\" = 'project1';";
            var rawTimestamp = Assert.IsType<string>(
                await rawTimestampCommand.ExecuteScalarAsync());
            Assert.False(rawTimestamp.EndsWith("Z", StringComparison.OrdinalIgnoreCase));
            Assert.False(
                rawTimestamp.Length >= 6
                && rawTimestamp[^6] is '+' or '-',
                $"Timestamp still contains an offset: {rawTimestamp}");
        }
        interceptor.Clear();

        var links = await repository.ListRecentAsync(
            10,
            createdAt.AddMinutes(1));

        Assert.Single(links);
        var command = Assert.Single(interceptor.ReaderCommands);
        Assert.Contains("\"CreatedAt\" <", command, StringComparison.Ordinal);
        Assert.DoesNotContain("IdempotencyKey", command, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdatedAt", command, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdatedBy", command, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnalyticsAndAuditReads_ProjectOnlyResponseFields()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var interceptor = new CommandCaptureInterceptor();
        var options = new DbContextOptionsBuilder<ShortLinkDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(interceptor)
            .Options;
        await using var dbContext = new ShortLinkDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        var occurredAt = new DateTimeOffset(2026, 8, 4, 5, 0, 0, TimeSpan.Zero);
        var clickRepository = new EfCoreShortLinkClickRepository(dbContext);
        var auditRepository = new EfCoreShortLinkAuditRepository(dbContext);
        await clickRepository.AddAsync(new ShortLinkClick(
            "project1",
            occurredAt,
            "127.0.0.1",
            "test-agent",
            null));
        await auditRepository.AddAsync(new AuditEvent(
            "user-1",
            ShortLinkAuditActions.Created,
            "project1",
            "user-1",
            occurredAt,
            targetType: ShortLinkAuditTargetTypes.ShortLink));

        interceptor.Clear();
        Assert.Single(await clickRepository.ListRecentAsync("project1", 10));
        var clickCommand = Assert.Single(interceptor.ReaderCommands);
        Assert.DoesNotContain("\"CreatedAt\"", clickCommand, StringComparison.Ordinal);
        Assert.DoesNotContain("\"UpdatedAt\"", clickCommand, StringComparison.Ordinal);

        interceptor.Clear();
        var auditPage = await auditRepository.ListAsync(new AuditQuery(
            10,
            null,
            null,
            null,
            new AuditReadScope(
                "admin",
                HasFullAccess: true,
                new HashSet<string>(StringComparer.Ordinal))));
        Assert.Single(auditPage.Items);
        var auditCommand = Assert.Single(interceptor.ReaderCommands);
        Assert.DoesNotContain("\"CreatedAt\"", auditCommand, StringComparison.Ordinal);
        Assert.DoesNotContain("\"UpdatedAt\"", auditCommand, StringComparison.Ordinal);
    }

    private sealed class CommandCaptureInterceptor : DbCommandInterceptor
    {
        public List<string> ReaderCommands { get; } = [];

        public void Clear() => ReaderCommands.Clear();

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            ReaderCommands.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}
