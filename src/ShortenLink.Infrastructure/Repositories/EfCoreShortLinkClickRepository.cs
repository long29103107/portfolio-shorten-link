using Microsoft.EntityFrameworkCore;
using ShortenLink.Core;
using ShortenLink.Core.Domain;
using ShortenLink.Infrastructure.Persistence;

namespace ShortenLink.Infrastructure.Repositories;

public sealed class EfCoreShortLinkClickRepository(ShortLinkDbContext dbContext)
    : EfCoreRepository<ShortLinkClickPersistenceEntity>(dbContext), IShortLinkClickRepository
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
}
