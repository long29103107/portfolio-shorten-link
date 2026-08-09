using ShortenLink.Infrastructure.Persistence;

namespace ShortenLink.Infrastructure.Persistence.Schema;

internal sealed class NoopSchemaDialect : IDatabaseSchemaDialect
{
    public Task EnsureUtcTimestampSchemaAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken) => Task.CompletedTask;

    public Task EnsureIdempotencySchemaAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken) => Task.CompletedTask;

    public Task EnsureScheduledActivationSchemaAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken) => Task.CompletedTask;

    public Task EnsureClickLimitSchemaAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken) => Task.CompletedTask;

    public Task EnsurePasswordProtectionSchemaAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken) => Task.CompletedTask;

    public Task EnsureAdvancedAnalyticsSchemaAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken) => Task.CompletedTask;

    public Task EnsureOrganizationSchemaAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken) => Task.CompletedTask;

    public Task EnsureAuditEventsTableAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken) => Task.CompletedTask;

    public Task EnsureExpirationCheckpointsTableAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken) => Task.CompletedTask;
}
