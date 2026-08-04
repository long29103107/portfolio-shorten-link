using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ShortenLink.Auditing;
using ShortenLink.Core.Contracts.Queries;
using ShortenLink.Core.Domain;
using ShortenLink.Infrastructure.Persistence;
using ShortenLink.Infrastructure.Repositories;
using Xunit;

namespace ShortenLink.Infrastructure.Tests;

public sealed class EfCoreShortLinkAuditRepositoryTests
{
    [Fact]
    public async Task ListAsync_AppliesScopeFiltersAndDeterministicCursor()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ShortLinkDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var dbContext = new ShortLinkDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        var repository = new EfCoreShortLinkAuditRepository(dbContext);
        var occurredAt = new DateTimeOffset(2026, 7, 25, 8, 0, 0, TimeSpan.Zero);
        var older = new AuditEvent(
            "owner-1",
            ShortLinkAuditActions.Created,
            "owned",
            "owner-1",
            occurredAt.AddMinutes(-1),
            id: Guid.Parse("00000000-0000-0000-0000-000000000001"),
            targetType: ShortLinkAuditTargetTypes.ShortLink);
        var newerLowId = new AuditEvent(
            "admin",
            ShortLinkAuditActions.Updated,
            "shared",
            "owner-2",
            occurredAt,
            id: Guid.Parse("00000000-0000-0000-0000-000000000002"),
            targetType: ShortLinkAuditTargetTypes.ShortLink);
        var newerHighId = new AuditEvent(
            "admin",
            ShortLinkAuditActions.Deleted,
            "hidden",
            "owner-3",
            occurredAt,
            id: Guid.Parse("00000000-0000-0000-0000-000000000003"),
            targetType: ShortLinkAuditTargetTypes.ShortLink);

        await repository.AddAsync(older);
        await repository.AddAsync(newerLowId);
        await repository.AddAsync(newerHighId);

        var userPage = await repository.ListAsync(new AuditQuery(
            10,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            new AuditReadScope(
                "owner-1",
                HasFullAccess: false,
                new HashSet<string>(["shared"], StringComparer.Ordinal))));
        var adminFirstPage = await repository.ListAsync(new AuditQuery(
            1,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            new AuditReadScope(
                "admin",
                HasFullAccess: true,
                new HashSet<string>(StringComparer.Ordinal))));
        var adminSecondPage = await repository.ListAsync(new AuditQuery(
            10,
            adminFirstPage.Items[0].OccurredAt,
            adminFirstPage.Items[0].Id,
            null,
            null,
            null,
            null,
            null,
            new AuditReadScope(
                "admin",
                HasFullAccess: true,
                new HashSet<string>(StringComparer.Ordinal))));
        var filteredPage = await repository.ListAsync(new AuditQuery(
            10,
            null,
            null,
            ShortLinkAuditActions.Updated,
            "shared",
            "admin",
            occurredAt,
            occurredAt,
            new AuditReadScope(
                "admin",
                HasFullAccess: true,
                new HashSet<string>(StringComparer.Ordinal))));

        Assert.Equal(["shared", "owned"], userPage.Items.Select(item => item.TargetId));
        Assert.Equal("hidden", Assert.Single(adminFirstPage.Items).TargetId);
        Assert.Equal(["shared", "owned"], adminSecondPage.Items.Select(item => item.TargetId));
        Assert.Equal("shared", Assert.Single(filteredPage.Items).TargetId);
    }

    [Fact]
    public async Task AuditEvent_RemainsAfterTargetLinkIsDeleted()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ShortLinkDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var dbContext = new ShortLinkDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        var linkRepository = new EfCoreShortLinkRepository(dbContext);
        var auditRepository = new EfCoreShortLinkAuditRepository(dbContext);
        var now = new DateTimeOffset(2026, 7, 25, 8, 0, 0, TimeSpan.Zero);

        await linkRepository.AddAsync(new ShortLink(
            "durable",
            new Uri("https://example.com"),
            now,
            now.AddDays(1),
            createdByUserId: "owner-1"));
        await auditRepository.AddAsync(new AuditEvent(
            "owner-1",
            ShortLinkAuditActions.Created,
            "durable",
            "owner-1",
            now,
            targetType: ShortLinkAuditTargetTypes.ShortLink));
        await linkRepository.DeleteAsync("durable");

        var page = await auditRepository.ListAsync(new AuditQuery(
            10,
            null,
            null,
            null,
            "durable",
            null,
            null,
            null,
            new AuditReadScope(
                "owner-1",
                HasFullAccess: false,
                new HashSet<string>(StringComparer.Ordinal))));

        Assert.Null(await linkRepository.FindByCodeAsync("durable"));
        Assert.Equal(ShortLinkAuditActions.Created, Assert.Single(page.Items).Action);
    }

    [Fact]
    public async Task ListAsync_UserSeesOwnIdentityEventsButSharedCodesOnlyExposeShortLinks()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ShortLinkDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var dbContext = new ShortLinkDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        var repository = new EfCoreShortLinkAuditRepository(dbContext);

        await repository.AddAsync(new AuditEvent(
            "user-1",
            ShortLinkAuditActions.AuthenticationLogin,
            "user-1",
            "user-1",
            DateTimeOffset.UnixEpoch,
            subjectId: "user-1",
            targetType: ShortLinkAuditTargetTypes.Authentication));
        await repository.AddAsync(new AuditEvent(
            "admin",
            ShortLinkAuditActions.SecurityUserUpdated,
            "user-1",
            ownerId: null,
            DateTimeOffset.UnixEpoch.AddSeconds(1),
            subjectId: "user-1",
            targetType: ShortLinkAuditTargetTypes.SecurityUser));
        await repository.AddAsync(new AuditEvent(
            "user-2",
            ShortLinkAuditActions.UserApiKeyCreated,
            "shared-code",
            "user-2",
            DateTimeOffset.UnixEpoch.AddSeconds(2),
            subjectId: "user-2",
            targetType: ShortLinkAuditTargetTypes.UserApiKey));
        await repository.AddAsync(new AuditEvent(
            "owner-2",
            ShortLinkAuditActions.Created,
            "shared-code",
            "owner-2",
            DateTimeOffset.UnixEpoch.AddSeconds(3),
            targetType: ShortLinkAuditTargetTypes.ShortLink));

        var page = await repository.ListAsync(new AuditQuery(
            10,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            new AuditReadScope(
                "user-1",
                HasFullAccess: false,
                new HashSet<string>(["shared-code"], StringComparer.Ordinal))));

        Assert.Equal(
            [ShortLinkAuditTargetTypes.ShortLink, ShortLinkAuditTargetTypes.Authentication],
            page.Items.Select(item => item.TargetType));
        Assert.DoesNotContain(
            page.Items,
            item => item.TargetType == ShortLinkAuditTargetTypes.SecurityUser);
        Assert.DoesNotContain(
            page.Items,
            item => item.TargetType == ShortLinkAuditTargetTypes.UserApiKey
                && item.OwnerId == "user-2");
    }
}
