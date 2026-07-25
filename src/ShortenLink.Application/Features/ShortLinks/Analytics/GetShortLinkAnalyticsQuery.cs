using ShortenLink.Core.Security;
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
        var summary = await clickRepository.GetSummaryAsync(request.Code, cancellationToken);
        var clicks = await clickRepository.ListRecentAsync(
            request.Code, Math.Clamp(request.Limit ?? 20, 1, 100), cancellationToken);
        return ShortLinkAnalyticsResponse.FromClicks(request.Code, summary, clicks);
    }
}
