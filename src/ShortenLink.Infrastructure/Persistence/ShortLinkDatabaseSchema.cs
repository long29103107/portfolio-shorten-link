using ShortenLink.Infrastructure.Persistence.Schema;

namespace ShortenLink.Infrastructure.Persistence;

/// <summary>
/// Stable compatibility boundary for databases created by earlier versions
/// that used EnsureCreated instead of migrations.
/// </summary>
public static class ShortLinkDatabaseSchema
{
    public static Task EnsureUtcTimestampSchemaAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken = default) =>
        GetDialect(dbContext).EnsureUtcTimestampSchemaAsync(dbContext, cancellationToken);

    public static Task EnsureIdempotencySchemaAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken = default) =>
        GetDialect(dbContext).EnsureIdempotencySchemaAsync(dbContext, cancellationToken);

    public static Task EnsureAuditEventsTableAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken = default) =>
        GetDialect(dbContext).EnsureAuditEventsTableAsync(dbContext, cancellationToken);

    public static Task EnsureExpirationCheckpointsTableAsync(
        ShortLinkDbContext dbContext,
        CancellationToken cancellationToken = default) =>
        GetDialect(dbContext).EnsureExpirationCheckpointsTableAsync(dbContext, cancellationToken);

    private static IDatabaseSchemaDialect GetDialect(ShortLinkDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        return DatabaseDialectResolver.Resolve(dbContext) switch
        {
            DatabaseDialect.Sqlite => new SqliteSchemaDialect(),
            DatabaseDialect.PostgreSql => new PostgresSchemaDialect(),
            _ => new NoopSchemaDialect()
        };
    }
}
