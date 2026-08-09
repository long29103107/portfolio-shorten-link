using ShortenLink.Core.Domain;
using ShortenLink.Core.Contracts.Responses;

namespace ShortenLink.Core.Abstractions;

public interface IShortLinkClickRepository
{
    Task AddAsync(ShortLinkClick shortLinkClick, CancellationToken cancellationToken = default);

    Task<ShortLinkClickSummaryResponse> GetSummaryAsync(
        string shortCode,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShortLinkClick>> ListRecentAsync(
        string shortCode,
        int limit,
        CancellationToken cancellationToken = default);
}

public interface ITenantAwareShortLinkClickRepository
{
    Task<ShortLinkClickSummaryResponse> GetSummaryAsync(
        string shortCode,
        string tenantId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShortLinkClick>> ListRecentAsync(
        string shortCode,
        string tenantId,
        int limit,
        CancellationToken cancellationToken = default);
}

public interface IAdvancedShortLinkClickRepository
{
    Task<ShortLinkClickAnalyticsSummary> GetAnalyticsAsync(
        string shortCode,
        CancellationToken cancellationToken = default);
}

public interface ITenantAwareAdvancedShortLinkClickRepository
{
    Task<ShortLinkClickAnalyticsSummary> GetAnalyticsAsync(
        string shortCode,
        string tenantId,
        CancellationToken cancellationToken = default);
}
