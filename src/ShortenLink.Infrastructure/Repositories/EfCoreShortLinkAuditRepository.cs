using Microsoft.EntityFrameworkCore;
using ShortenLink.Core.Abstractions;
using ShortenLink.Core.Contracts.Queries;
using ShortenLink.Core.Domain;
using ShortenLink.Infrastructure.Persistence;
using ShortenLink.Infrastructure.Persistence.ReadModels;
using ShortenLink.Core.Querying;

namespace ShortenLink.Infrastructure.Repositories;

public sealed class EfCoreShortLinkAuditRepository(ShortLinkDbContext dbContext)
    : EfCoreRepository<AuditEventPersistenceEntity>(dbContext), IAuditRepository
{
    private static readonly string[] FilterableProperties =
    [
        nameof(AuditEventPersistenceEntity.ActorId),
        nameof(AuditEventPersistenceEntity.Action),
        nameof(AuditEventPersistenceEntity.TargetType),
        nameof(AuditEventPersistenceEntity.TargetId),
        nameof(AuditEventPersistenceEntity.Outcome),
        nameof(AuditEventPersistenceEntity.OccurredAt),
        nameof(AuditEventPersistenceEntity.SubjectUserId)
    ];

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

        var filtered = ApplyAccessScope(ReadOnlyEntities, query.ReadScope)
            .ApplyFilter(query.FilterExpression, FilterableProperties);

        if (query.BeforeOccurredAt is not null)
        {
            filtered = filtered.Where(record => record.OccurredAt < query.BeforeOccurredAt
                || (record.OccurredAt == query.BeforeOccurredAt
                    && query.BeforeId != null
                    && record.Id.CompareTo(query.BeforeId.Value) < 0));
        }

        var records = await filtered
            .OrderByDescending(record => record.OccurredAt)
            .ThenByDescending(record => record.Id)
            .Take(Math.Clamp(query.Limit, 1, 200))
            .Select(AuditEventPersistenceReadModel.Projection)
            .ToListAsync(cancellationToken);

        return new AuditPage(
            records.Select(static record => record.ToDomain()).ToList());
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
