using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ShortenLink.Infrastructure.Persistence;
using Xunit;

namespace ShortenLink.Infrastructure.Tests;

public sealed class ShortLinkDatabaseSchemaTests
{
    [Fact]
    public async Task EnsureAuditEventsTable_RecreatesAuditTableInLegacyDatabase()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ShortLinkDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new ShortLinkDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        await dbContext.Database.ExecuteSqlRawAsync("DROP TABLE \"short_link_audit_events\"");

        await ShortLinkDatabaseSchema.EnsureAuditEventsTableAsync(dbContext);

        Assert.Equal(0, await dbContext.AuditEvents.CountAsync());

        var indexes = await ReadIndexNamesAsync(connection);
        Assert.Contains("IX_short_link_audit_events_OccurredAt_Id", indexes);
        Assert.Contains("IX_short_link_audit_events_Action", indexes);
        Assert.Contains("IX_short_link_audit_events_TargetId", indexes);
        Assert.Contains("IX_short_link_audit_events_ActorId", indexes);
        Assert.Contains("IX_short_link_audit_events_OwnerUserId", indexes);
    }

    [Fact]
    public async Task EnsureCompatibilitySchema_IsIdempotentForFreshDatabase()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ShortLinkDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new ShortLinkDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        for (var attempt = 0; attempt < 2; attempt++)
        {
            await ShortLinkDatabaseSchema.EnsureAuditEventsTableAsync(dbContext);
            await ShortLinkDatabaseSchema.EnsureExpirationCheckpointsTableAsync(dbContext);
            await ShortLinkDatabaseSchema.EnsureIdempotencySchemaAsync(dbContext);
            await ShortLinkDatabaseSchema.EnsureScheduledActivationSchemaAsync(dbContext);
            await ShortLinkDatabaseSchema.EnsureUtcTimestampSchemaAsync(dbContext);
        }

        Assert.Equal(0, await dbContext.AuditEvents.CountAsync());
        Assert.Equal(0, await dbContext.ShortLinkExpirationCheckpoints.CountAsync());

        var indexes = await ReadIndexNamesAsync(connection, "short_link_audit_events");
        var checkpointIndexes = await ReadIndexNamesAsync(
            connection,
            "short_link_expiration_checkpoints");
        Assert.Contains("IX_short_link_audit_events_OccurredAt_Id", indexes);
        Assert.Contains("IX_short_link_expiration_checkpoints_TenantId", checkpointIndexes);
    }

    private static async Task<HashSet<string>> ReadIndexNamesAsync(
        SqliteConnection connection,
        string tableName = "short_link_audit_events")
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT name FROM sqlite_master WHERE type = 'index' AND tbl_name = '{tableName}'";
        await using var reader = await command.ExecuteReaderAsync();
        var names = new HashSet<string>(StringComparer.Ordinal);
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }
}
