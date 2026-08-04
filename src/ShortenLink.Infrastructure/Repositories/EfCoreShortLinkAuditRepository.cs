using Microsoft.EntityFrameworkCore;
using ShortenLink.Core.Abstractions;
using ShortenLink.Core.Contracts.Queries;
using ShortenLink.Core.Domain;
using ShortenLink.Infrastructure.Persistence;

namespace ShortenLink.Infrastructure.Repositories;

public sealed class EfCoreShortLinkAuditRepository(ShortLinkDbContext dbContext)
    : EfCoreRepository<AuditEventPersistenceEntity>(dbContext), IAuditRepository
{
    public async Task AddAsync(
        AuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        await AddEntityAsync(
            AuditEventPersistenceEntity.FromDomain(auditEvent),
            cancellationToken);
    }

    public async Task<AuditPage> ListAsync(
        AuditQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var sqliteDateQuery = DbContext.Database.IsSqlite();
        var filteredSource = sqliteDateQuery && query.BeforeOccurredAt is null
            ? ApplySqliteDateRange(ReadOnlyEntities, query.From, query.To)
            : ReadOnlyEntities;
        var filtered = ApplyFilters(
            ApplyAccessScope(filteredSource, query.ReadScope),
            query,
            includeDateFilters: !sqliteDateQuery);

        if (query.BeforeOccurredAt is not null)
        {
            if (DbContext.Database.IsSqlite())
            {
                var cursor = query.BeforeOccurredAt.Value;
                var safeLimit = Math.Clamp(query.Limit, 1, 200);
                var equal = await filtered
                    .Where(record => record.OccurredAt == cursor)
                    .OrderByDescending(record => record.Id)
                    .ToListAsync(cancellationToken);
                equal = equal
                    .Where(record => query.From is null || record.OccurredAt >= query.From)
                    .Where(record => query.To is null || record.OccurredAt <= query.To)
                    .ToList();
                var selected = query.BeforeId is null
                    ? []
                    : equal
                        .Where(record => record.Id.CompareTo(query.BeforeId.Value) < 0)
                        .Take(safeLimit)
                        .ToList();

                if (selected.Count < safeLimit)
                {
                    var remaining = safeLimit - selected.Count;
                    var olderSource = ApplySqliteDateRange(
                        DbContext.Set<AuditEventPersistenceEntity>(),
                        query.From,
                        query.To,
                        lessThan: cursor);
                    var older = await ApplyFilters(ApplyAccessScope(olderSource, query.ReadScope), query, includeDateFilters: false)
                        .OrderByDescending(record => record.OccurredAt.ToString())
                        .ThenByDescending(record => record.Id)
                        .Take(remaining)
                        .ToListAsync(cancellationToken);
                    selected.AddRange(older);
                }

                return new AuditPage(selected.Select(record => record.ToDomain()).ToList());
            }

            filtered = filtered.Where(record => record.OccurredAt < query.BeforeOccurredAt
                || (record.OccurredAt == query.BeforeOccurredAt
                    && query.BeforeId != null
                    && record.Id.CompareTo(query.BeforeId.Value) < 0));
        }

        var page = await filtered
            .OrderByDescending(record => record.OccurredAt.ToString())
            .ThenByDescending(record => record.Id)
            .Take(Math.Clamp(query.Limit, 1, 200))
            .Select(record => record.ToDomain())
            .ToListAsync(cancellationToken);

        return new AuditPage(page);
    }

    private static IQueryable<AuditEventPersistenceEntity> ApplyFilters(
        IQueryable<AuditEventPersistenceEntity> query,
        AuditQuery filter,
        bool includeDateFilters = true)
    {
        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            query = query.Where(record => record.Action == filter.Action);
        }

        if (!string.IsNullOrWhiteSpace(filter.TargetId))
        {
            query = query.Where(record => record.TargetId == filter.TargetId);
        }

        if (!string.IsNullOrWhiteSpace(filter.ActorId))
        {
            query = query.Where(record => record.ActorId == filter.ActorId);
        }

        if (includeDateFilters && filter.From is not null)
        {
            query = query.Where(record => record.OccurredAt >= filter.From);
        }

        if (includeDateFilters && filter.To is not null)
        {
            query = query.Where(record => record.OccurredAt <= filter.To);
        }

        return query;
    }

    public async Task<IReadOnlyList<string>> ListActionsAsync(
        AuditReadScope accessScope,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyAccessScope(ReadOnlyEntities, accessScope);
        return await query
            .Select(record => record.Action)
            .Distinct()
            .OrderBy(action => action)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<AuditEventPersistenceEntity> ApplySqliteDateRange(
        IQueryable<AuditEventPersistenceEntity> query,
        DateTimeOffset? from,
        DateTimeOffset? to,
        DateTimeOffset? lessThan = null)
    {
        if (lessThan is not null)
        {
            return (from, to) switch
            {
                (not null, not null) => DbContext.Set<AuditEventPersistenceEntity>()
                    .FromSqlInterpolated($"SELECT * FROM short_link_audit_events WHERE OccurredAt < {lessThan.Value} AND OccurredAt >= {from!.Value} AND OccurredAt <= {to!.Value}"),
                (not null, null) => DbContext.Set<AuditEventPersistenceEntity>()
                    .FromSqlInterpolated($"SELECT * FROM short_link_audit_events WHERE OccurredAt < {lessThan.Value} AND OccurredAt >= {from!.Value}"),
                (null, not null) => DbContext.Set<AuditEventPersistenceEntity>()
                    .FromSqlInterpolated($"SELECT * FROM short_link_audit_events WHERE OccurredAt < {lessThan.Value} AND OccurredAt <= {to!.Value}"),
                _ => DbContext.Set<AuditEventPersistenceEntity>()
                    .FromSqlInterpolated($"SELECT * FROM short_link_audit_events WHERE OccurredAt < {lessThan.Value}")
            };
        }

        return (from, to) switch
        {
            (not null, not null) => DbContext.Set<AuditEventPersistenceEntity>()
                .FromSqlInterpolated($"SELECT * FROM short_link_audit_events WHERE OccurredAt >= {from!.Value} AND OccurredAt <= {to!.Value}"),
            (not null, null) => DbContext.Set<AuditEventPersistenceEntity>()
                .FromSqlInterpolated($"SELECT * FROM short_link_audit_events WHERE OccurredAt >= {from!.Value}"),
            (null, not null) => DbContext.Set<AuditEventPersistenceEntity>()
                .FromSqlInterpolated($"SELECT * FROM short_link_audit_events WHERE OccurredAt <= {to!.Value}"),
            _ => query
        };
    }

    private static IQueryable<AuditEventPersistenceEntity> ApplyAccessScope(
        IQueryable<AuditEventPersistenceEntity> query,
        AuditReadScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (scope.HasFullAccess)
        {
            return query;
        }

        if (string.IsNullOrWhiteSpace(scope.PrincipalId))
        {
            return query.Where(_ => false);
        }

        var sharedCodes = scope.AccessibleTargetIds.ToArray();
        return query.Where(record => record.OwnerUserId == scope.PrincipalId
            || (record.TargetType == ShortLinkAuditTargetTypes.ShortLink
                && sharedCodes.Contains(record.TargetId)));
    }

}
