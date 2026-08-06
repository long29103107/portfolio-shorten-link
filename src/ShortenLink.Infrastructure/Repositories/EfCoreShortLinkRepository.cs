using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Npgsql;
using ShortenLink.Infrastructure.Persistence;
using ShortenLink.Infrastructure.Persistence.ReadModels;
using ShortenLink.Core.Security;
using ShortenLink.Core.Querying;

namespace ShortenLink.Infrastructure.Repositories;

public sealed class EfCoreShortLinkRepository(ShortLinkDbContext dbContext)
      : EfCoreRepository<ShortLinkPersistenceEntity>(dbContext),
      IShortLinkRepository,
      IShortLinkIdempotencyRepository,
      IShortLinkTenantRepository,
      IShortLinkExpirationRepository
{
    private static readonly string[] FilterableProperties =
    [
        nameof(ShortLinkPersistenceEntity.Code),
        nameof(ShortLinkPersistenceEntity.OriginalUrl),
        nameof(ShortLinkPersistenceEntity.ExpiresAt),
        nameof(ShortLinkPersistenceEntity.IsActive),
        nameof(ShortLinkPersistenceEntity.CreatedAt),
        nameof(ShortLinkPersistenceEntity.CreatedByUserId)
    ];

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
                .Select(ShortLinkPersistenceReadModel.Projection)
                .ToListAsync(cancellationToken);
            var selected = equalTimestamp
                .Where(link => string.CompareOrdinal(link.Code, beforeCode) > 0)
                .Take(safeLimit)
                .ToList();

            if (selected.Count < safeLimit)
            {
                var remaining = safeLimit - selected.Count;
                var older = await query
                    .Where(link => link.CreatedAt < cursorCreatedAt)
                    .OrderByDescending(link => link.CreatedAt)
                    .ThenBy(link => link.Code)
                    .Take(remaining)
                    .Select(ShortLinkPersistenceReadModel.Projection)
                    .ToListAsync(cancellationToken);
                selected.AddRange(older);
            }

            return selected.Select(record => record.ToDomain()).ToList();
        }

        query = ApplyRecentCursor(query, beforeCreatedAt, beforeCode);

        return await MaterializeReadModelsAsync(query
            .OrderByDescending(link => link.CreatedAt)
            .ThenBy(link => link.Code)
            .Take(safeLimit), cancellationToken);
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
                .Select(ShortLinkPersistenceReadModel.Projection)
                .ToListAsync(cancellationToken);
            var selected = equalTimestamp
                .Where(link => string.CompareOrdinal(link.Code, beforeCode) > 0)
                .Take(safeLimit)
                .ToList();

            if (selected.Count < safeLimit)
            {
                var remaining = safeLimit - selected.Count;
                var newer = await query
                    .Where(link => link.ExpiresAt > cursorExpiresAt)
                    .OrderBy(link => link.ExpiresAt)
                    .ThenBy(link => link.Code)
                    .Take(remaining)
                    .Select(ShortLinkPersistenceReadModel.Projection)
                    .ToListAsync(cancellationToken);
                selected.AddRange(newer);
            }

            return selected.Select(record => record.ToDomain()).ToList();
        }

        query = ApplyExpirationCursor(query, beforeExpiresAt, beforeCode);

        return await MaterializeReadModelsAsync(query
            .OrderBy(record => record.ExpiresAt)
            .ThenBy(record => record.Code)
            .Take(safeLimit), cancellationToken);
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
                    .Select(ShortLinkPersistenceReadModel.Projection)
                    .ToListAsync(cancellationToken);
                var selected = equalTimestamp
                    .Where(link => string.CompareOrdinal(link.Code, beforeCode) > 0)
                    .Take(Math.Clamp(limit, 1, 500))
                    .ToList();
                if (selected.Count < Math.Clamp(limit, 1, 500))
                {
                    var remaining = Math.Clamp(limit, 1, 500) - selected.Count;
                    var older = await query
                        .Where(link => link.CreatedAt < cursorCreatedAt)
                        .OrderByDescending(link => link.CreatedAt)
                        .ThenBy(link => link.Code)
                        .Take(remaining)
                        .Select(ShortLinkPersistenceReadModel.Projection)
                        .ToListAsync(cancellationToken);
                    selected.AddRange(older);
                }

                return selected.Select(record => record.ToDomain()).ToList();
            }

            query = query.Where(link => link.CreatedAt < cursorCreatedAt);
        }
        else
        {
            query = ApplyRecentCursor(query, beforeCreatedAt, beforeCode);
        }

        return await MaterializeReadModelsAsync(query
            .OrderByDescending(link => link.CreatedAt)
            .ThenBy(link => link.Code)
            .Take(Math.Clamp(limit, 1, 500)), cancellationToken);
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
        return await MaterializeReadModelsAsync(ReadOnlyEntities
            .Where(record => record.TenantId == string.Empty)
            .OrderByDescending(link => link.CreatedAt)
            .ThenBy(link => link.Code)
            .Skip(safeSkip)
            .Take(safeLimit), cancellationToken);
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
            && query.SortDirection == ShortLinkSortDirection.Desc;
        var safeLimit = Math.Clamp(limit, 1, cursorMode ? 501 : 500);
        var statusScoped = ApplyProviderCreatedCursor(
            ReadOnlyEntities,
            query,
            cursorMode,
            out var cursorAppliedInSql);

        var filtered = ApplyAccessScope(statusScoped, query.AccessScope);
        filtered = filtered.ApplyFilter(query.FilterExpression, FilterableProperties);
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

        var ordered = MaterializeReadModelsAsync(
            orderedQuery.Take(safeLimit),
            cancellationToken);

        return new ShortLinkListPage(await ordered, totalCount);
    }

    private IQueryable<ShortLinkPersistenceEntity> ApplyProviderCreatedCursor(
        IQueryable<ShortLinkPersistenceEntity> source,
        ShortLinkListQuery query,
        bool cursorMode,
        out bool cursorApplied)
    {
        cursorApplied = cursorMode
            && DbContext.Database.IsSqlite();
        if (!cursorApplied)
        {
            return source;
        }

        var cursorCreatedAt = query.BeforeCreatedAt!.Value.UtcDateTime;
        return string.IsNullOrWhiteSpace(query.BeforeCode)
            ? DbContext.Set<ShortLinkPersistenceEntity>()
                .FromSqlInterpolated(
                    $"SELECT * FROM short_links WHERE CreatedAt < {cursorCreatedAt}")
            : DbContext.Set<ShortLinkPersistenceEntity>()
                .FromSqlInterpolated(
                    $"SELECT * FROM short_links WHERE CreatedAt < {cursorCreatedAt} OR (CreatedAt = {cursorCreatedAt} AND Code > {query.BeforeCode})");
    }

    private static async Task<IReadOnlyList<ShortLink>> MaterializeReadModelsAsync(
        IQueryable<ShortLinkPersistenceEntity> query,
        CancellationToken cancellationToken)
    {
        var records = await query
            .Select(ShortLinkPersistenceReadModel.Projection)
            .ToListAsync(cancellationToken);
        return records.Select(static record => record.ToDomain()).ToList();
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

    private static IQueryable<ShortLinkPersistenceEntity> ApplySort(
        IQueryable<ShortLinkPersistenceEntity> query,
        ShortLinkListQuery listQuery)
    {
        var descending = listQuery.SortDirection == ShortLinkSortDirection.Desc;
        return listQuery.SortBy switch
        {
            ShortLinkListSortBy.Expiry => descending
                ? query.OrderByDescending(record => record.ExpiresAt == null)
                    .ThenByDescending(record => record.ExpiresAt)
                    .ThenBy(record => record.Code)
                : query.OrderBy(record => record.ExpiresAt == null)
                    .ThenBy(record => record.ExpiresAt)
                    .ThenBy(record => record.Code),
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
                ? query.OrderByDescending(record => record.CreatedAt).ThenBy(record => record.Code)
                : query.OrderBy(record => record.CreatedAt).ThenBy(record => record.Code)
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
