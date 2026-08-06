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

    Task EnsureAuditEventsTableAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken);

    Task EnsureExpirationCheckpointsTableAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken);
}
