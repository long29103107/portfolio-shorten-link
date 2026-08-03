using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ShortenLink.Core.Abstractions;
using ShortenLink.Core.Contracts.Expiration;
using ShortenLink.Infrastructure.Persistence;
using ShortenLink.Infrastructure.Repositories;
using Xunit;

namespace ShortenLink.Infrastructure.Tests;

public sealed class EfCoreShortLinkExpirationCheckpointRepositoryTests
{
    [Fact]
    public async Task SaveAsync_UpsertsPerTenantAndPreservesPartitionIsolation()
    {
        await using var database = new SqliteTestDatabase();
        var repository = database.CreateRepository();
        var instant = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);

        await repository.SaveAsync(new ShortLinkExpirationCheckpoint(
            "tenant-a", "cursor-a", instant, instant));
        await repository.SaveAsync(new ShortLinkExpirationCheckpoint(
            "tenant-a", "cursor-b", instant, instant.AddMinutes(1)));
        await repository.SaveAsync(new ShortLinkExpirationCheckpoint(
            "tenant-b", "cursor-c", instant, instant));

        var tenantA = await repository.FindAsync("tenant-a");
        var tenantB = await repository.FindAsync("tenant-b");

        Assert.Equal("cursor-b", tenantA!.Cursor);
        Assert.Equal("cursor-c", tenantB!.Cursor);
        Assert.Equal("tenant-a", tenantA.TenantId);
    }

    private sealed class SqliteTestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");

        public SqliteTestDatabase()
        {
            connection.Open();
            using var context = CreateContext();
            context.Database.EnsureCreated();
        }

        public EfCoreShortLinkExpirationCheckpointRepository CreateRepository() =>
            new(CreateContext());

        private ShortLinkDbContext CreateContext() =>
            new(new DbContextOptionsBuilder<ShortLinkDbContext>()
                .UseSqlite(connection)
                .Options);

        public async ValueTask DisposeAsync() => await connection.DisposeAsync();
    }
}
