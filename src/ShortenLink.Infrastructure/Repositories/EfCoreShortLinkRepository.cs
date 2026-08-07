using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Npgsql;
using ShortenLink.Infrastructure.Persistence;

namespace ShortenLink.Infrastructure.Repositories;

public sealed partial class EfCoreShortLinkRepository(
    ShortLinkDbContext dbContext)
    : EfCoreRepository<ShortLinkPersistenceEntity>(dbContext),
      IShortLinkRepository,
      IShortLinkIdempotencyRepository,
      IShortLinkTenantRepository,
      IShortLinkExpirationRepository
{
    public async Task AddAsync(
        ShortLink shortLink,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shortLink);

        var persistenceEntity = ShortLinkPersistenceEntity.FromDomain(shortLink);
        try
        {
            await AddEntityAsync(persistenceEntity, cancellationToken);
        }
        catch (DbUpdateException exception) when (IsIdempotencyConflict(exception))
        {
            DbContext.Entry(persistenceEntity).State = EntityState.Detached;
            throw new ShortLinkIdempotencyConflictException(exception);
        }
        catch (DbUpdateException exception) when (IsCodeConflict(exception))
        {
            DbContext.Entry(persistenceEntity).State = EntityState.Detached;
            throw new ShortLinkCodeConflictException(shortLink.Code, exception);
        }
    }

    public async Task UpdateAsync(
        ShortLink shortLink,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shortLink);

        var record = await Entities
            .FirstOrDefaultAsync(link => link.Code == shortLink.Code, cancellationToken);

        if (record is null)
            Entities.Add(ShortLinkPersistenceEntity.FromDomain(shortLink));
        else
            record.UpdateFromDomain(shortLink);

        await SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var record = await Entities
            .FirstOrDefaultAsync(link => link.Code == code, cancellationToken);

        if (record is not null)
        {
            Entities.Remove(record);
            await SaveChangesAsync(cancellationToken);
        }
    }

    private static bool IsCodeConflict(DbUpdateException exception)
    {
        var providerException = exception.InnerException;
        if (providerException is SqliteException sqliteException)
        {
            return sqliteException.SqliteErrorCode == 19
                && sqliteException.Message.Contains(
                    "short_links.Code",
                    StringComparison.OrdinalIgnoreCase);
        }

        if (providerException is PostgresException postgresException)
        {
            return postgresException.SqlState == PostgresErrorCodes.UniqueViolation
                && string.Equals(
                    postgresException.ConstraintName,
                    "IX_short_links_Code",
                    StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool IsIdempotencyConflict(DbUpdateException exception)
    {
        var providerException = exception.InnerException;
        if (providerException is SqliteException sqliteException)
        {
            return sqliteException.SqliteErrorCode == 19
                && sqliteException.Message.Contains(
                    "short_links.IdempotencyKey",
                    StringComparison.OrdinalIgnoreCase);
        }

        if (providerException is PostgresException postgresException)
        {
            return postgresException.SqlState == PostgresErrorCodes.UniqueViolation
                && string.Equals(
                    postgresException.ConstraintName,
                    "IX_short_links_TenantId_IdempotencyKey",
                    StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
