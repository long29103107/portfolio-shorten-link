using ShortenLink.Infrastructure.Persistence;

namespace ShortenLink.Infrastructure.Persistence.Schema;

internal interface IDatabaseSchemaDialect
{
    Task EnsureUtcTimestampSchemaAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken);

    Task EnsureIdempotencySchemaAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken);

    Task EnsureScheduledActivationSchemaAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken);

    Task EnsureClickLimitSchemaAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken);

    Task EnsurePasswordProtectionSchemaAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken);

    Task EnsureAdvancedAnalyticsSchemaAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken);

    Task EnsureOrganizationSchemaAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken);

    Task EnsureAuditEventsTableAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken);

    Task EnsureExpirationCheckpointsTableAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken);

    Task EnsureBulkJobsTableAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken);
}
