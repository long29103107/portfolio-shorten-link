using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using ShortenLink.Core.Domain;
using ShortenLink.Infrastructure.Persistence;
using Xunit;

namespace ShortenLink.Infrastructure.Tests;

public sealed class ShortLinkDbContextModelTests
{
    [Fact]
    public void EveryDbSetEntity_UsesGuidBaseEntityAndGuidPrimaryKey()
    {
        var options = new DbContextOptionsBuilder<ShortLinkDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using var dbContext = new ShortLinkDbContext(options);

        var entityTypes = dbContext.Model.GetEntityTypes()
            .Where(entityType => entityType.ClrType is not null)
            .ToList();

        Assert.Equal(10, entityTypes.Count);
        Assert.All(entityTypes, entityType =>
        {
            Assert.True(
                typeof(BaseEntity<Guid>).IsAssignableFrom(entityType.ClrType),
                $"{entityType.ClrType.Name} must inherit BaseEntity<Guid>.");
            var primaryKey = Assert.Single(entityType.FindPrimaryKey()!.Properties);
            Assert.Equal(nameof(BaseEntity<Guid>.Id), primaryKey.Name);
            Assert.Equal(typeof(Guid), primaryKey.ClrType);
        });
    }

    [Fact]
    public async Task EnsureCreated_UsesIdAsPrimaryKeyForEveryPersistedTable()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ShortLinkDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var dbContext = new ShortLinkDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var tables = new[]
        {
            "short_links",
            "short_link_clicks",
            "short_link_shares",
            "short_link_audit_events",
            "short_link_expiration_checkpoints",
            "shorten_link_security_assignments",
            "shorten_link_security_custom_roles",
            "shorten_link_security_role_permission_overrides",
            "shorten_link_security_users",
            "shorten_link_security_user_api_keys"
        };

        foreach (var table in tables)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info(\"{table}\");";
            await using var reader = await command.ExecuteReaderAsync();
            var primaryKeys = new List<string>();
            while (await reader.ReadAsync())
            {
                if (reader.GetInt32(5) > 0)
                {
                    primaryKeys.Add(reader.GetString(1));
                }
            }

            Assert.Equal(new[] { "Id" }, primaryKeys);
        }
    }
}
