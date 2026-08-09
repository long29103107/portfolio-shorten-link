using ShortenLink.Core.Security;
using ShortenLink.Core.Exceptions;
using ShortenLink.Core.Abstractions;
using ShortenLink.Core.Contracts.Responses;
using ShortenLink.Core.Domain;
using ShortenLink.Core.Services;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Features.ShortLinks.Analytics;

public sealed record GetShortLinkAnalyticsQuery(
    string Code,
    int? Limit) : IRequest<ShortLinkAnalyticsResponse>;

internal sealed class GetShortLinkAnalyticsQueryHandler(
    IShortLinkService shortLinkService,
    IShortLinkClickRepository clickRepository,
    ShortLinkAccessGuard accessGuard)
    : IRequestHandler<GetShortLinkAnalyticsQuery, ShortLinkAnalyticsResponse>
{
    public async Task<ShortLinkAnalyticsResponse> Handle(
        GetShortLinkAnalyticsQuery request,
        CancellationToken cancellationToken)
    {
        var user = await accessGuard.GetAuthorizedUserAsync(
            ShortenLinkPermissionCatalog.AnalyticsRead, cancellationToken);
        var link = ShortLinkFeatureSupport.GetRequired(
            await shortLinkService.GetDetailsAsync(request.Code, cancellationToken));
        await accessGuard.EnsureAccessAsync(
            link, user, ShortLinkShareAccess.View, false,
            "You do not have access to this short link.", cancellationToken);
        ShortLinkClickSummaryResponse summary;
        ShortLinkClickAnalyticsSummary? advancedSummary = null;
        IReadOnlyList<ShortLinkClickEntity> clicks;
        if (link.TenantId is null)
        {
            if (clickRepository is IAdvancedShortLinkClickRepository advancedRepository)
            {
                advancedSummary = await advancedRepository.GetAnalyticsAsync(
                    request.Code,
                    cancellationToken);
                summary = new ShortLinkClickSummaryResponse(
                    request.Code,
                    advancedSummary.ClickCount,
                    advancedSummary.LastClickedAtUtc);
            }
            else
            {
                summary = await clickRepository.GetSummaryAsync(request.Code, cancellationToken);
            }
            clicks = await clickRepository.ListRecentAsync(
                request.Code, Math.Clamp(request.Limit ?? 20, 1, 100), cancellationToken);
        }
        else if (clickRepository is ITenantAwareShortLinkClickRepository tenantRepository)
        {
            if (clickRepository is ITenantAwareAdvancedShortLinkClickRepository advancedRepository)
            {
                advancedSummary = await advancedRepository.GetAnalyticsAsync(
                    request.Code,
                    link.TenantId,
                    cancellationToken);
                summary = new ShortLinkClickSummaryResponse(
                    request.Code,
                    advancedSummary.ClickCount,
                    advancedSummary.LastClickedAtUtc);
            }
            else
            {
                summary = await tenantRepository.GetSummaryAsync(
                    request.Code,
                    link.TenantId,
                    cancellationToken);
            }
            clicks = await tenantRepository.ListRecentAsync(
                request.Code, link.TenantId, Math.Clamp(request.Limit ?? 20, 1, 100), cancellationToken);
        }
        else
        {
            throw new BusinessRuleException(
                ShortLinkErrorCodes.TenantNotSupported,
                "The configured analytics provider does not support tenant partitions.");
        }
        return ShortLinkAnalyticsResponse.FromClicks(
            request.Code,
            summary,
            clicks,
            advancedSummary);
    }
}
