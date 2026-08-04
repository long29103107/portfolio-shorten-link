using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Npgsql;
using ShortenLink.Core.Domain;
using ShortenLink.Core.Exceptions;
using ShortenLink.Core.Services;
using ShortenLink.Infrastructure.Persistence;
using ShortenLink.Core.Security;

namespace ShortenLink.Infrastructure.Repositories;

public sealed class EfCoreShortLinkRepository(ShortLinkDbContext dbContext)
      : EfCoreRepository<ShortLinkPersistenceEntity>(dbContext),
      IShortLinkRepository,
      IShortLinkIdempotencyRepository,
      IShortLinkTenantRepository,
      IShortLinkExpirationRepository
{
    public async Task<IReadOnlyList<ShortLink>> ListRecentAsync(
        int limit,
        DateTimeOffset? beforeCreatedAt = null,
        string? beforeCode = null,
        CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var query = ReadOnlyEntities
            .Where(record => record.TenantId == string.Empty);

        // SQLite cannot translate a lexicographic string comparison. Keep the
        // timestamp and page-size bounds in SQL, and only scan the single
        // cursor timestamp bucket in memory for the deterministic code tie-breaker.
        if (beforeCreatedAt is not null && !string.IsNullOrWhiteSpace(beforeCode))
        {
            var cursorCreatedAt = beforeCreatedAt.Value;
            var equalTimestamp = await query
                .Where(link => link.CreatedAt == cursorCreatedAt)
                .OrderBy(link => link.Code)
                .ToListAsync(cancellationToken);
            var selected = equalTimestamp
                .Where(link => string.CompareOrdinal(link.Code, beforeCode) > 0)
                .Take(safeLimit)
                .ToList();

            if (selected.Count < safeLimit)
            {
                var remaining = safeLimit - selected.Count;
                var older = await (DbContext.Database.IsSqlite()
                    ? DbContext.Set<ShortLinkPersistenceEntity>()
                        .FromSqlInterpolated($"SELECT * FROM short_links WHERE TenantId = {string.Empty} AND CreatedAt < {cursorCreatedAt}")
                    : query.Where(link => link.CreatedAt < cursorCreatedAt))
                    .OrderByDescending(link => link.CreatedAt.ToString())
                    .ThenBy(link => link.Code)
                    .Take(remaining)
                    .ToListAsync(cancellationToken);
                selected.AddRange(older);
            }

            return selected.Select(record => record.ToDomain()).ToList();
        }

        if (beforeCreatedAt is not null && DbContext.Database.IsSqlite())
        {
            query = DbContext.Set<ShortLinkPersistenceEntity>()
                .FromSqlInterpolated($"SELECT * FROM short_links WHERE TenantId = {string.Empty} AND CreatedAt < {beforeCreatedAt.Value}");
        }
        else
        {
            query = ApplyRecentCursor(query, beforeCreatedAt, beforeCode);
        }

        return await query
            .OrderByDescending(link => link.CreatedAt.ToString())
            .ThenBy(link => link.Code)
            .Take(safeLimit)
            .Select(record => record.ToDomain())
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ShortLink>> ListExpirationCandidatesAsync(
        string? tenantId,
        DateTimeOffset? beforeExpiresAt,
        string? beforeCode,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 501);
        var tenantKey = tenantId ?? string.Empty;
        var query = ReadOnlyEntities
            .Where(record => record.TenantId == tenantKey && record.ExpiresAt != null)
            ;

        // SQLite cannot translate a lexicographic string comparison. Keep the
        // timestamp and page-size bounds in SQL, and only scan the single
        // cursor timestamp bucket in memory for the deterministic code tie-breaker.
        if (beforeExpiresAt is not null && !string.IsNullOrWhiteSpace(beforeCode))
        {
            var cursorExpiresAt = beforeExpiresAt.Value;
            var equalTimestamp = await query
                .Where(link => link.ExpiresAt == cursorExpiresAt)
                .OrderBy(link => link.Code)
                .ToListAsync(cancellationToken);
            var selected = equalTimestamp
                .Where(link => string.CompareOrdinal(link.Code, beforeCode) > 0)
                .Take(safeLimit)
                .ToList();

            if (selected.Count < safeLimit)
            {
                var remaining = safeLimit - selected.Count;
                var newer = await DbContext.Set<ShortLinkPersistenceEntity>()
                    .FromSqlInterpolated($"SELECT * FROM short_links WHERE TenantId = {tenantKey} AND ExpiresAt IS NOT NULL AND ExpiresAt > {cursorExpiresAt}")
                    .OrderBy(link => link.ExpiresAt!.Value.ToString())
                    .ThenBy(link => link.Code)
                    .Take(remaining)
                    .ToListAsync(cancellationToken);
                selected.AddRange(newer);
            }

            return selected.Select(record => record.ToDomain()).ToList();
        }

        if (beforeExpiresAt is not null && DbContext.Database.IsSqlite())
        {
            query = DbContext.Set<ShortLinkPersistenceEntity>()
                .FromSqlInterpolated($"SELECT * FROM short_links WHERE TenantId = {tenantKey} AND ExpiresAt IS NOT NULL AND ExpiresAt > {beforeExpiresAt.Value}");
        }
        else
        {
            query = ApplyExpirationCursor(query, beforeExpiresAt, beforeCode);
        }

        return await query
            .OrderBy(record => record.ExpiresAt!.Value.ToString())
            .ThenBy(record => record.Code)
            .Take(safeLimit)
            .Select(record => record.ToDomain())
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ShortLink>> ListAccessibleRecentAsync(
        int limit,
        DateTimeOffset? beforeCreatedAt,
        string? beforeCode,
        ShortLinkAccessScope accessScope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accessScope);
        var query = ApplyAccessScope(ReadOnlyEntities, accessScope);

        if (beforeCreatedAt is not null && DbContext.Database.IsSqlite())
        {
            var cursorCreatedAt = beforeCreatedAt.Value;
            if (!string.IsNullOrWhiteSpace(beforeCode))
            {
                var equalTimestamp = await query
                    .Where(link => link.CreatedAt == cursorCreatedAt)
                    .OrderBy(link => link.Code)
                    .ToListAsync(cancellationToken);
                var selected = equalTimestamp
                    .Where(link => string.CompareOrdinal(link.Code, beforeCode) > 0)
                    .Take(Math.Clamp(limit, 1, 500))
                    .ToList();
                if (selected.Count < Math.Clamp(limit, 1, 500))
                {
                    var remaining = Math.Clamp(limit, 1, 500) - selected.Count;
                    var olderSource = DbContext.Set<ShortLinkPersistenceEntity>()
                        .FromSqlInterpolated($"SELECT * FROM short_links WHERE TenantId = {accessScope.TenantId ?? string.Empty} AND CreatedAt < {cursorCreatedAt}");
                    var older = await ApplyAccessScope(olderSource, accessScope)
                        .OrderByDescending(link => link.CreatedAt.ToString())
                        .ThenBy(link => link.Code)
                        .Take(remaining)
                        .ToListAsync(cancellationToken);
                    selected.AddRange(older);
                }

                return selected.Select(record => record.ToDomain()).ToList();
            }

            var source = DbContext.Set<ShortLinkPersistenceEntity>()
                .FromSqlInterpolated($"SELECT * FROM short_links WHERE TenantId = {accessScope.TenantId ?? string.Empty} AND CreatedAt < {cursorCreatedAt}");
            query = ApplyAccessScope(source, accessScope);
        }
        else
        {
            query = ApplyRecentCursor(query, beforeCreatedAt, beforeCode);
        }

        return await query
            .OrderByDescending(link => link.CreatedAt.ToString())
            .ThenBy(link => link.Code)
            .Take(Math.Clamp(limit, 1, 500))
            .Select(record => record.ToDomain())
            .ToListAsync(cancellationToken);
    }

    private IQueryable<ShortLinkPersistenceEntity> ApplyRecentCursor(
        IQueryable<ShortLinkPersistenceEntity> query,
        DateTimeOffset? beforeCreatedAt,
        string? beforeCode)
    {
        if (beforeCreatedAt is null)
        {
            return query;
        }

        var cursorCreatedAt = beforeCreatedAt.Value;
        if (string.IsNullOrWhiteSpace(beforeCode))
        {
            return query.Where(link => link.CreatedAt < cursorCreatedAt);
        }

        return query.Where(link => link.CreatedAt < cursorCreatedAt
            || (link.CreatedAt == cursorCreatedAt
                && string.Compare(link.Code, beforeCode) > 0));
    }

    private static IQueryable<ShortLinkPersistenceEntity> ApplyExpirationCursor(
        IQueryable<ShortLinkPersistenceEntity> query,
        DateTimeOffset? beforeCreatedAt,
        string? beforeCode)
    {
        if (beforeCreatedAt is null && string.IsNullOrWhiteSpace(beforeCode))
        {
            return query;
        }

        var cursorExpiresAt = beforeCreatedAt ?? DateTimeOffset.MaxValue;
        return query.Where(link => link.ExpiresAt > cursorExpiresAt);
    }

    public async Task<IReadOnlyList<ShortLink>> ListRecentPageAsync(
        int skip,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var safeSkip = Math.Max(skip, 0);
        var safeLimit = Math.Clamp(limit, 1, 500);
        return await ReadOnlyEntities
            .Where(record => record.TenantId == string.Empty)
            .OrderByDescending(link => link.CreatedAt.ToString())
            .ThenBy(link => link.Code)
            .Skip(safeSkip)
            .Take(safeLimit)
            .Select(record => record.ToDomain())
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        Entities.CountAsync(link => link.TenantId == string.Empty, cancellationToken);

    public async Task<ShortLinkListPage> ListPageAsync(
        int skip,
        int limit,
        ShortLinkListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var safeSkip = Math.Max(skip, 0);
        var cursorMode = query.BeforeCreatedAt is not null
            && query.SortBy == ShortLinkListSortBy.Created
            && query.SortDirection == ShortLinkSortDirection.Desc
            && (!DbContext.Database.IsSqlite() || query.Status == ShortLinkListStatus.All);
        var safeLimit = Math.Clamp(limit, 1, cursorMode ? 501 : 500);
        var statusScoped = ReadOnlyEntities;
        var statusAppliedInSql = false;
        var cursorAppliedInSql = false;
        if (cursorMode && query.Status == ShortLinkListStatus.All && DbContext.Database.IsSqlite())
        {
            var cursorCreatedAt = query.BeforeCreatedAt!.Value;
            statusScoped = string.IsNullOrWhiteSpace(query.BeforeCode)
                ? DbContext.Set<ShortLinkPersistenceEntity>()
                    .FromSqlInterpolated($"SELECT * FROM short_links WHERE CreatedAt < {cursorCreatedAt}")
                : DbContext.Set<ShortLinkPersistenceEntity>()
                    .FromSqlInterpolated($"SELECT * FROM short_links WHERE CreatedAt < {cursorCreatedAt} OR (CreatedAt = {cursorCreatedAt} AND Code > {query.BeforeCode})");
            cursorAppliedInSql = true;
        }
        else if (DbContext.Database.IsSqlite())
        {
            statusScoped = ApplySqliteStatusBoundary(statusScoped, query, out statusAppliedInSql);
        }

        var filtered = ApplyAccessScope(statusScoped, query.AccessScope);
        filtered = ApplySearch(filtered, query.Search);
        if (!statusAppliedInSql)
        {
            filtered = ApplyStatus(filtered, query);
        }
        var totalCount = await filtered.CountAsync(cancellationToken);
        var orderedQuery = ApplySort(filtered, query);
        if (cursorMode && !cursorAppliedInSql)
        {
            orderedQuery = orderedQuery.Where(record =>
                record.CreatedAt < query.BeforeCreatedAt!.Value
                || (record.CreatedAt == query.BeforeCreatedAt.Value
                    && string.Compare(record.Code, query.BeforeCode) > 0));
        }
        else if (!cursorMode)
        {
            orderedQuery = orderedQuery.Skip(safeSkip);
        }

        var ordered = orderedQuery
            .Take(safeLimit)
            .Select(record => record.ToDomain())
            .ToListAsync(cancellationToken);

        return new ShortLinkListPage(await ordered, totalCount);
    }

    private IQueryable<ShortLinkPersistenceEntity> ApplySqliteStatusBoundary(
        IQueryable<ShortLinkPersistenceEntity> query,
        ShortLinkListQuery listQuery,
        out bool statusApplied)
    {
        statusApplied = listQuery.Status is ShortLinkListStatus.Active
            or ShortLinkListStatus.Expired
            or ShortLinkListStatus.ExpiringSoon;
        if (!statusApplied)
        {
            return query;
        }

        return listQuery.Status switch
        {
            ShortLinkListStatus.Active => DbContext.Set<ShortLinkPersistenceEntity>()
                .FromSqlInterpolated($"SELECT * FROM short_links WHERE IsActive = 1 AND (ExpiresAt IS NULL OR ExpiresAt > {listQuery.Now})"),
            ShortLinkListStatus.Expired => DbContext.Set<ShortLinkPersistenceEntity>()
                .FromSqlInterpolated($"SELECT * FROM short_links WHERE IsActive = 1 AND ExpiresAt IS NOT NULL AND ExpiresAt <= {listQuery.Now}"),
            _ => DbContext.Set<ShortLinkPersistenceEntity>()
                .FromSqlInterpolated($"SELECT * FROM short_links WHERE IsActive = 1 AND ExpiresAt > {listQuery.Now} AND ExpiresAt <= {listQuery.ExpiringSoonBefore}")
        };
    }

    private static IQueryable<ShortLinkPersistenceEntity> ApplyAccessScope(
        IQueryable<ShortLinkPersistenceEntity> query,
        ShortLinkAccessScope? accessScope)
    {
        if (accessScope is null)
        {
            return query.Where(record => record.TenantId == string.Empty);
        }

        var tenantId = accessScope.TenantId ?? string.Empty;
        query = query.Where(record => record.TenantId == tenantId);
        if (accessScope.IsAdmin)
        {
            return query;
        }

        var sharedCodes = accessScope.SharedAccess.Keys.ToArray();
        var userId = accessScope.UserId;
        return string.IsNullOrWhiteSpace(userId)
            ? query.Where(record => sharedCodes.Contains(record.Code))
            : query.Where(record => record.CreatedByUserId == userId
                || record.SharingMode == ShortLinkSharingMode.Public
                || sharedCodes.Contains(record.Code));
    }

    private static IQueryable<ShortLinkPersistenceEntity> ApplySearch(
        IQueryable<ShortLinkPersistenceEntity> query,
        string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return query;
        }

        var normalizedSearch = search.ToLowerInvariant();
        return query.Where(record => record.Code.ToLower().Contains(normalizedSearch)
            || record.OriginalUrl.ToLower().Contains(normalizedSearch));
    }

    private static IQueryable<ShortLinkPersistenceEntity> ApplyStatus(
        IQueryable<ShortLinkPersistenceEntity> query,
        ShortLinkListQuery listQuery) =>
        listQuery.Status switch
        {
            ShortLinkListStatus.Active => query.Where(record => record.IsActive
                && (record.ExpiresAt == null || record.ExpiresAt > listQuery.Now)),
            ShortLinkListStatus.Inactive => query.Where(record => !record.IsActive),
            ShortLinkListStatus.Expired => query.Where(record => record.IsActive
                && record.ExpiresAt != null
                && record.ExpiresAt <= listQuery.Now),
            ShortLinkListStatus.ExpiringSoon => query.Where(record => record.IsActive
                && record.ExpiresAt != null
                && record.ExpiresAt > listQuery.Now
                && record.ExpiresAt <= listQuery.ExpiringSoonBefore),
            _ => query
        };

    private static IQueryable<ShortLinkPersistenceEntity> ApplySort(
        IQueryable<ShortLinkPersistenceEntity> query,
        ShortLinkListQuery listQuery)
    {
        var descending = listQuery.SortDirection == ShortLinkSortDirection.Desc;
        return listQuery.SortBy switch
        {
            ShortLinkListSortBy.Expiry => descending
                ? query.OrderByDescending(record => (record.ExpiresAt ?? DateTimeOffset.MaxValue).ToString()).ThenBy(record => record.Code)
                : query.OrderBy(record => (record.ExpiresAt ?? DateTimeOffset.MaxValue).ToString()).ThenBy(record => record.Code),
            ShortLinkListSortBy.Destination => descending
                ? query.OrderByDescending(record => record.OriginalUrl).ThenBy(record => record.Code)
                : query.OrderBy(record => record.OriginalUrl).ThenBy(record => record.Code),
            ShortLinkListSortBy.Code => descending
                ? query.OrderByDescending(record => record.Code).ThenBy(record => record.Code)
                : query.OrderBy(record => record.Code).ThenBy(record => record.Code),
            ShortLinkListSortBy.Status => descending
                ? query.OrderByDescending(record => !record.IsActive
                    ? 2
                    : record.ExpiresAt != null && record.ExpiresAt <= listQuery.Now ? 1 : 0)
                    .ThenBy(record => record.Code)
                : query.OrderBy(record => !record.IsActive
                    ? 2
                    : record.ExpiresAt != null && record.ExpiresAt <= listQuery.Now ? 1 : 0)
                    .ThenBy(record => record.Code),
            _ => descending
                ? query.OrderByDescending(record => record.CreatedAt.ToString()).ThenBy(record => record.Code)
                : query.OrderBy(record => record.CreatedAt.ToString()).ThenBy(record => record.Code)
        };
    }

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
            .FirstOrDefaultAsync(
                link => link.TenantId == string.Empty && link.IdempotencyKey == idempotencyKey,
                cancellationToken);

        return record?.ToDomain();
    }

    public async Task<ShortLink?> FindByTenantIdempotencyKeyAsync(
        string tenantId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        var record = await ReadOnlyEntities.FirstOrDefaultAsync(
            link => link.TenantId == tenantId && link.IdempotencyKey == idempotencyKey,
            cancellationToken);
        return record?.ToDomain();
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
                    "IX_short_links_TenantId_IdempotencyKey",
                    StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
