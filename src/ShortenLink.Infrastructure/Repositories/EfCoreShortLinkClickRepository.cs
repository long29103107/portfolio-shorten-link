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

    public async Task<ShortLinkClickSummaryResponse> GetSummaryAsync(
        string shortCode,
        CancellationToken cancellationToken = default)
    {
        ShortCodeValidator.ValidateCodeOrThrow(shortCode);

        var query = ReadOnlyEntities
            .Where(click => click.ShortCode == shortCode);
        var clickCount = await query.LongCountAsync(cancellationToken);
        var lastClickedAtUtc = clickCount == 0
            ? null
            : await query
                .OrderByDescending(click => click.ClickedAtUtc.ToString())
                .Select(click => (DateTimeOffset?)click.ClickedAtUtc)
                .FirstAsync(cancellationToken);

        return new ShortLinkClickSummaryResponse(shortCode, clickCount, lastClickedAtUtc);
    }

    public Task<ShortLinkClickSummaryResponse> GetSummaryAsync(
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
        return await ReadOnlyEntities
            .Where(click => click.ShortCode == shortCode)
            .OrderByDescending(click => click.ClickedAtUtc.ToString())
            .ThenByDescending(click => click.Id)
            .Take(safeLimit)
            .Select(record => record.ToDomain())
            .ToListAsync(cancellationToken);
    }

    public Task<IReadOnlyList<ShortLinkClick>> ListRecentAsync(
        string shortCode,
        string tenantId,
        int limit,
        CancellationToken cancellationToken = default) =>
        ListRecentCoreAsync(shortCode, tenantId, limit, cancellationToken);

    private async Task<ShortLinkClickSummaryResponse> GetSummaryCoreAsync(
        string shortCode,
        string tenantId,
        CancellationToken cancellationToken)
    {
        ShortCodeValidator.ValidateCodeOrThrow(shortCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        var query = ReadOnlyEntities.Where(click => click.ShortCode == shortCode && click.TenantId == tenantId);
        var clickCount = await query.LongCountAsync(cancellationToken);
        var lastClickedAtUtc = clickCount == 0
            ? null
            : await query
                .OrderByDescending(click => click.ClickedAtUtc.ToString())
                .Select(click => (DateTimeOffset?)click.ClickedAtUtc)
                .FirstAsync(cancellationToken);
        return new ShortLinkClickSummaryResponse(
            shortCode,
            clickCount,
            lastClickedAtUtc);
    }

    private async Task<IReadOnlyList<ShortLinkClick>> ListRecentCoreAsync(
        string shortCode,
        string tenantId,
        int limit,
        CancellationToken cancellationToken)
    {
        ShortCodeValidator.ValidateCodeOrThrow(shortCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        return await ReadOnlyEntities
            .Where(click => click.ShortCode == shortCode && click.TenantId == tenantId)
            .OrderByDescending(click => click.ClickedAtUtc.ToString())
            .ThenByDescending(click => click.Id)
            .Take(Math.Clamp(limit, 1, 100))
            .Select(record => record.ToDomain())
            .ToListAsync(cancellationToken);
    }
}
