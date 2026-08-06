using Microsoft.EntityFrameworkCore;
using ShortenLink.Infrastructure.Persistence;

namespace ShortenLink.Infrastructure.Persistence.Schema;

internal enum DatabaseDialect
{
    Unsupported,
    Sqlite,
    PostgreSql
}

internal static class DatabaseDialectResolver
{
    public static DatabaseDialect Resolve(ShortLinkDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        if (dbContext.Database.IsSqlite())
        {
            return DatabaseDialect.Sqlite;
        }

        if (dbContext.Database.IsNpgsql())
        {
            return DatabaseDialect.PostgreSql;
        }

        return DatabaseDialect.Unsupported;
    }
}
