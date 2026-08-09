using Microsoft.EntityFrameworkCore;
using ShortenLink.Core.Abstractions;
using ShortenLink.Infrastructure.Persistence.Entities;

namespace ShortenLink.Infrastructure.Repositories;

public sealed partial class EfCoreShortLinkRepository : IShortLinkClickLimitRepository
{
    public async Task<ShortLinkClickConsumptionResult> TryConsumeClickAsync(
        string code,
        string? tenantId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var tenantKey = tenantId ?? string.Empty;
        var record = await Entities.AsNoTracking().FirstOrDefaultAsync(
            link => link.Code == code && link.TenantId == tenantKey,
            cancellationToken);
        if (record is null)
        {
            return ShortLinkClickConsumptionResult.NotFound;
        }

        var currentResult = Classify(record, now);
        if (currentResult is ShortLinkClickConsumptionResult.NotLimited
            or ShortLinkClickConsumptionResult.LimitReached
            or ShortLinkClickConsumptionResult.Inactive
            or ShortLinkClickConsumptionResult.Scheduled
            or ShortLinkClickConsumptionResult.Expired)
        {
            if (currentResult != ShortLinkClickConsumptionResult.NotLimited)
            {
                return currentResult;
            }

            return ShortLinkClickConsumptionResult.NotLimited;
        }

        var affected = await Entities
            .Where(link => link.Id == record.Id
                && link.IsActive
                && (link.ActiveFrom == null || link.ActiveFrom <= now)
                && (link.ExpiresAt == null || link.ExpiresAt > now)
                && link.MaxClicks != null
                && link.ClickCount < link.MaxClicks)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(link => link.ClickCount, link => link.ClickCount + 1)
                .SetProperty(
                    link => link.IsActive,
                    link => link.MaxClicks != null && link.ClickCount + 1 >= link.MaxClicks
                        ? false
                        : link.IsActive),
                cancellationToken);

        if (affected == 1)
        {
            return ShortLinkClickConsumptionResult.Consumed;
        }

        record = await Entities.AsNoTracking().FirstOrDefaultAsync(
            link => link.Code == code && link.TenantId == tenantKey,
            cancellationToken);
        return record is null
            ? ShortLinkClickConsumptionResult.NotFound
            : Classify(record, now);
    }

    private static ShortLinkClickConsumptionResult Classify(
        ShortLinkPersistenceEntity record,
        DateTimeOffset now)
    {
        if (record.MaxClicks is not null && record.ClickCount >= record.MaxClicks)
        {
            return ShortLinkClickConsumptionResult.LimitReached;
        }

        if (!record.IsActive)
        {
            return ShortLinkClickConsumptionResult.Inactive;
        }

        if (record.ActiveFrom is not null && record.ActiveFrom > now)
        {
            return ShortLinkClickConsumptionResult.Scheduled;
        }

        if (record.ExpiresAt is not null && record.ExpiresAt <= now)
        {
            return ShortLinkClickConsumptionResult.Expired;
        }

        return record.MaxClicks is null
            ? ShortLinkClickConsumptionResult.NotLimited
            : ShortLinkClickConsumptionResult.Consumed;
    }
}
