using Microsoft.EntityFrameworkCore;
using ShortenLink.Core.Abstractions;
using ShortenLink.Core.Contracts.Queries;
using ShortenLink.Core.Domain;
using ShortenLink.Infrastructure.Persistence;

namespace ShortenLink.Infrastructure.Repositories;

public sealed class EfCoreShortLinkAuditRepository(ShortLinkDbContext dbContext)
    : EfCoreRepository<ShortLinkAuditEventPersistenceEntity>(dbContext), IShortLinkAuditRepository
{
    public async Task AddAsync(
        ShortLinkAuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        await AddEntityAsync(
            ShortLinkAuditEventPersistenceEntity.FromDomain(auditEvent),
            cancellationToken);
    }

    public async Task<ShortLinkAuditPage> ListAsync(
        ShortLinkAuditQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var records = await ReadOnlyEntities
            .ToListAsync(cancellationToken);

        var filtered = records
            .Where(record => IsAccessible(record, query.AccessScope))
            .Where(record => string.IsNullOrWhiteSpace(query.Action)
                || string.Equals(record.Action, query.Action, StringComparison.Ordinal))
            .Where(record => string.IsNullOrWhiteSpace(query.TargetId)
                || string.Equals(record.TargetId, query.TargetId, StringComparison.Ordinal))
            .Where(record => string.IsNullOrWhiteSpace(query.ActorId)
                || string.Equals(record.ActorId, query.ActorId, StringComparison.Ordinal))
            .Where(record => query.From is null || record.OccurredAt >= query.From)
            .Where(record => query.To is null || record.OccurredAt <= query.To)
            .OrderByDescending(record => record.OccurredAt)
            .ThenByDescending(record => record.Id)
            .Where(record => IsAfterCursor(record, query.BeforeOccurredAt, query.BeforeId))
            .Take(Math.Clamp(query.Limit, 1, 200))
            .Select(record => record.ToDomain())
            .ToList();

        return new ShortLinkAuditPage(filtered);
    }

    public async Task<IReadOnlyList<string>> ListActionsAsync(
        ShortLinkAuditAccessScope accessScope,
        CancellationToken cancellationToken = default)
    {
        var records = await ReadOnlyEntities.ToListAsync(cancellationToken);
        return records
            .Where(record => IsAccessible(record, accessScope))
            .Select(record => record.Action)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(action => action, StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsAccessible(
        ShortLinkAuditEventPersistenceEntity record,
        ShortLinkAuditAccessScope scope)
    {
        if (scope.IsAdmin)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(scope.UserId))
        {
            return false;
        }

        return string.Equals(record.OwnerUserId, scope.UserId, StringComparison.Ordinal)
            || (string.Equals(
                    record.TargetType,
                    ShortLinkAuditTargetTypes.ShortLink,
                    StringComparison.Ordinal)
                && scope.SharedShortCodes.Contains(record.TargetId));
    }

    private static bool IsAfterCursor(
        ShortLinkAuditEventPersistenceEntity record,
        DateTimeOffset? beforeOccurredAt,
        Guid? beforeId)
    {
        if (beforeOccurredAt is null)
        {
            return true;
        }

        return record.OccurredAt < beforeOccurredAt
            || (record.OccurredAt == beforeOccurredAt
                && beforeId is not null
                && record.Id.CompareTo(beforeId.Value) < 0);
    }
}
