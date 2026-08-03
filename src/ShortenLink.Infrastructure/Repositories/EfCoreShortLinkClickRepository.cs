using Microsoft.EntityFrameworkCore;
using ShortenLink.Core;
using ShortenLink.Core.Domain;
using ShortenLink.Infrastructure.Persistence;

namespace ShortenLink.Infrastructure.Repositories;

public sealed class EfCoreShortLinkClickRepository(ShortLinkDbContext dbContext)
    : EfCoreRepository<ShortLinkClickPersistenceEntity>(dbContext), IShortLinkClickRepository
      , ITenantAwareShortLinkClickRepository
{
    public async Task AddAsync(
        ShortLinkClick shortLinkClick,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shortLinkClick);

        await AddEntityAsync(
            ShortLinkClickPersistenceEntity.FromDomain(shortLinkClick),
            cancellationToken);
    }

    public async Task<ShortLinkClickSummary> GetSummaryAsync(
        string shortCode,
        CancellationToken cancellationToken = default)
    {
        ShortCodeValidator.ValidateCodeOrThrow(shortCode);

        var query = ReadOnlyEntities
            .Where(click => click.ShortCode == shortCode);
        var clickCount = await query.LongCountAsync(cancellationToken);
        var clickedAtValues = await query
            .Select(click => click.ClickedAtUtc)
            .ToListAsync(cancellationToken)
            ;
        DateTimeOffset? lastClickedAtUtc = clickedAtValues.Count == 0
            ? null
            : clickedAtValues.Max();

        return new ShortLinkClickSummary(shortCode, clickCount, lastClickedAtUtc);
    }

    public Task<ShortLinkClickSummary> GetSummaryAsync(
        string shortCode,
        string tenantId,
        CancellationToken cancellationToken = default) =>
        GetSummaryCoreAsync(shortCode, tenantId, cancellationToken);

    public async Task<IReadOnlyList<ShortLinkClick>> ListRecentAsync(
        string shortCode,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ShortCodeValidator.ValidateCodeOrThrow(shortCode);

        var safeLimit = Math.Clamp(limit, 1, 100);
        var records = await ReadOnlyEntities
            .Where(click => click.ShortCode == shortCode)
            .ToListAsync(cancellationToken)
            ;

        return records
            .OrderByDescending(click => click.ClickedAtUtc)
            .ThenByDescending(click => click.Id)
            .Take(safeLimit)
            .Select(record => record.ToDomain())
            .ToList();
    }

    public Task<IReadOnlyList<ShortLinkClick>> ListRecentAsync(
        string shortCode,
        string tenantId,
        int limit,
        CancellationToken cancellationToken = default) =>
        ListRecentCoreAsync(shortCode, tenantId, limit, cancellationToken);

    private async Task<ShortLinkClickSummary> GetSummaryCoreAsync(
        string shortCode,
        string tenantId,
        CancellationToken cancellationToken)
    {
        ShortCodeValidator.ValidateCodeOrThrow(shortCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        var query = ReadOnlyEntities.Where(click => click.ShortCode == shortCode && click.TenantId == tenantId);
        var clickCount = await query.LongCountAsync(cancellationToken);
        var clickedAtValues = await query.Select(click => click.ClickedAtUtc).ToListAsync(cancellationToken);
        return new ShortLinkClickSummary(
            shortCode,
            clickCount,
            clickedAtValues.Count == 0 ? null : clickedAtValues.Max());
    }

    private async Task<IReadOnlyList<ShortLinkClick>> ListRecentCoreAsync(
        string shortCode,
        string tenantId,
        int limit,
        CancellationToken cancellationToken)
    {
        ShortCodeValidator.ValidateCodeOrThrow(shortCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        var records = await ReadOnlyEntities
            .Where(click => click.ShortCode == shortCode && click.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        return records
            .OrderByDescending(click => click.ClickedAtUtc)
            .ThenByDescending(click => click.Id)
            .Take(Math.Clamp(limit, 1, 100))
            .Select(record => record.ToDomain())
            .ToList();
    }
}
