using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Npgsql;
using ShortenLink.Core.Domain;
using ShortenLink.Core.Exceptions;
using ShortenLink.Core.Services;
using ShortenLink.Infrastructure.Persistence;

namespace ShortenLink.Infrastructure.Repositories;

public sealed class EfCoreShortLinkRepository(ShortLinkDbContext dbContext)
    : EfCoreRepository<ShortLinkPersistenceEntity>(dbContext), IShortLinkRepository, IShortLinkIdempotencyRepository
{
    public async Task<IReadOnlyList<ShortLink>> ListRecentAsync(
        int limit,
        DateTimeOffset? beforeCreatedAt = null,
        string? beforeCode = null,
        CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);

        var records = await ReadOnlyEntities
            .ToListAsync(cancellationToken)
            ;

        return records
            .OrderByDescending(link => link.CreatedAt)
            .ThenBy(link => link.Code, StringComparer.Ordinal)
            .Where(link => IsAfterCursor(link, beforeCreatedAt, beforeCode))
            .Take(safeLimit)
            .Select(record => record.ToDomain())
            .ToList();
    }

    public async Task<IReadOnlyList<ShortLink>> ListAccessibleRecentAsync(
        int limit,
        DateTimeOffset? beforeCreatedAt,
        string? beforeCode,
        ShortLinkAccessScope accessScope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accessScope);
        var records = await ReadOnlyEntities
            .ToListAsync(cancellationToken)
            ;
        return records
            .Where(record => IsAccessible(record, accessScope))
            .OrderByDescending(link => link.CreatedAt)
            .ThenBy(link => link.Code, StringComparer.Ordinal)
            .Where(link => IsAfterCursor(link, beforeCreatedAt, beforeCode))
            .Take(Math.Clamp(limit, 1, 500))
            .Select(record => record.ToDomain())
            .ToList();
    }

    private static bool IsAfterCursor(
        ShortLinkPersistenceEntity link,
        DateTimeOffset? beforeCreatedAt,
        string? beforeCode)
    {
        if (beforeCreatedAt is null)
        {
            return true;
        }

        if (link.CreatedAt < beforeCreatedAt)
        {
            return true;
        }

        return link.CreatedAt == beforeCreatedAt
            && !string.IsNullOrWhiteSpace(beforeCode)
            && string.Compare(link.Code, beforeCode, StringComparison.Ordinal) > 0;
    }

    public async Task<IReadOnlyList<ShortLink>> ListRecentPageAsync(
        int skip,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var safeSkip = Math.Max(skip, 0);
        var safeLimit = Math.Clamp(limit, 1, 500);
        var records = await ReadOnlyEntities
            .ToListAsync(cancellationToken)
            ;

        return records
            .OrderByDescending(link => link.CreatedAt)
            .ThenBy(link => link.Code, StringComparer.Ordinal)
            .Skip(safeSkip)
            .Take(safeLimit)
            .Select(record => record.ToDomain())
            .ToList();
    }

    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        Entities.CountAsync(cancellationToken);

    public async Task<ShortLinkListPage> ListPageAsync(
        int skip,
        int limit,
        ShortLinkListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var safeSkip = Math.Max(skip, 0);
        var safeLimit = Math.Clamp(limit, 1, 500);
        var records = await ReadOnlyEntities
            .ToListAsync(cancellationToken)
            ;

        var filtered = records
            .Where(record => query.AccessScope is null || IsAccessible(record, query.AccessScope))
            .Where(record => MatchesSearch(record, query.Search))
            .Where(record => MatchesStatus(record, query))
            .ToList();
        var ordered = ApplySort(filtered, query)
            .Skip(safeSkip)
            .Take(safeLimit)
            .Select(record => record.ToDomain())
            .ToList();

        return new ShortLinkListPage(ordered, filtered.Count);
    }

    private static bool IsAccessible(ShortLinkPersistenceEntity record, ShortLinkAccessScope accessScope) =>
        accessScope.IsAdmin
        || (!string.IsNullOrWhiteSpace(accessScope.UserId)
            && string.Equals(record.CreatedByUserId, accessScope.UserId, StringComparison.Ordinal))
        || accessScope.SharedAccess.ContainsKey(record.Code);

    public async Task<ShortLink?> FindByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var record = await ReadOnlyEntities
            .FirstOrDefaultAsync(link => link.Code == code, cancellationToken)
            ;

        return record?.ToDomain();
    }

    public async Task<ShortLink?> FindByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        var record = await ReadOnlyEntities
            .FirstOrDefaultAsync(link => link.IdempotencyKey == idempotencyKey, cancellationToken);

        return record?.ToDomain();
    }

    private static bool MatchesSearch(ShortLinkPersistenceEntity record, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        return record.Code.Contains(search, StringComparison.OrdinalIgnoreCase)
            || record.OriginalUrl.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesStatus(ShortLinkPersistenceEntity record, ShortLinkListQuery query) =>
        query.Status switch
        {
            ShortLinkListStatus.Active => record.IsActive && !IsExpired(record, query.Now),
            ShortLinkListStatus.Inactive => !record.IsActive,
            ShortLinkListStatus.Expired => record.IsActive && IsExpired(record, query.Now),
            ShortLinkListStatus.ExpiringSoon => record.IsActive
                && !IsExpired(record, query.Now)
                && record.ExpiresAt is not null
                && record.ExpiresAt <= query.ExpiringSoonBefore,
            _ => true
        };

    private static IEnumerable<ShortLinkPersistenceEntity> ApplySort(
        IEnumerable<ShortLinkPersistenceEntity> records,
        ShortLinkListQuery query)
    {
        return query.SortBy switch
        {
            ShortLinkListSortBy.Expiry => ApplyDirection(
                records,
                query.SortDirection,
                record => record.ExpiresAt ?? DateTimeOffset.MaxValue),
            ShortLinkListSortBy.Destination => ApplyDirection(
                records,
                query.SortDirection,
                record => record.OriginalUrl),
            ShortLinkListSortBy.Code => ApplyDirection(
                records,
                query.SortDirection,
                record => record.Code),
            ShortLinkListSortBy.Status => ApplyDirection(
                records,
                query.SortDirection,
                record => GetStatusRank(record, query.Now)),
            _ => ApplyDirection(
                records,
                query.SortDirection,
                record => record.CreatedAt)
        };
    }

    private static IEnumerable<ShortLinkPersistenceEntity> ApplyDirection<TKey>(
        IEnumerable<ShortLinkPersistenceEntity> records,
        ShortLinkSortDirection direction,
        Func<ShortLinkPersistenceEntity, TKey> keySelector)
    {
        return direction == ShortLinkSortDirection.Asc
            ? records.OrderBy(keySelector).ThenBy(record => record.Code, StringComparer.Ordinal)
            : records.OrderByDescending(keySelector).ThenBy(record => record.Code, StringComparer.Ordinal);
    }

    private static bool IsExpired(ShortLinkPersistenceEntity record, DateTimeOffset now) =>
        record.ExpiresAt is not null && record.ExpiresAt <= now;

    private static int GetStatusRank(ShortLinkPersistenceEntity record, DateTimeOffset now)
    {
        if (!record.IsActive)
        {
            return 2;
        }

        return IsExpired(record, now) ? 1 : 0;
    }

    public Task<bool> ExistsByCodeAsync(
        string code,
        CancellationToken cancellationToken = default) =>
        Entities.AnyAsync(link => link.Code == code, cancellationToken);

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
            .FirstOrDefaultAsync(link => link.Code == shortLink.Code, cancellationToken)
            ;

        if (record is null)
        {
            Entities.Add(ShortLinkPersistenceEntity.FromDomain(shortLink));
        }
        else
        {
            record.UpdateFromDomain(shortLink);
        }

        await SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var record = await Entities
            .FirstOrDefaultAsync(link => link.Code == code, cancellationToken)
            ;

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
                    "IX_short_links_IdempotencyKey",
                    StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
