using ShortenLink.Core.Security;
using ShortenLink.Mediator;
using ShortLinkDetailsResponse = ShortenLink.Application.Contracts.Responses.ShortLinkDetailsResponse;

namespace ShortenLink.Application.Features.ShortLinks.Details;

public sealed record GetShortLinkDetailsQuery(
    string Code) : IRequest<ShortLinkDetailsResponse>;

internal sealed class GetShortLinkDetailsQueryHandler(
    IShortLinkService shortLinkService,
    ShortLinkAccessGuard accessGuard)
    : IRequestHandler<GetShortLinkDetailsQuery, ShortLinkDetailsResponse>
{
    public async Task<ShortLinkDetailsResponse> Handle(
        GetShortLinkDetailsQuery request,
        CancellationToken cancellationToken)
    {
        var user = await accessGuard.GetAuthorizedUserAsync(
            ShortenLinkPermissionCatalog.ShortLinksRead, cancellationToken);
        var link = ShortLinkFeatureSupport.GetRequired(
            await shortLinkService.GetDetailsAsync(request.Code, cancellationToken));
        await accessGuard.EnsureAccessAsync(
            link, user, ShortLinkShareAccess.View, false,
            "You do not have access to this short link.", cancellationToken);
        return ShortLinkDetailsResponse.FromDomain(link);
    }
}
