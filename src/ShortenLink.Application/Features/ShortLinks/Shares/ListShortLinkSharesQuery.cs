using ShortenLink.Core.Security;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Features.ShortLinks.Shares;

public sealed record ListShortLinkSharesQuery(
    string Code) : IRequest<ShortLinkSharesResponse>;

internal sealed class ListShortLinkSharesQueryHandler(
    IShortLinkService shortLinkService,
    IShortLinkShareRepository shareRepository,
    IShortenLinkSecurityUserRepository userRepository,
    ShortLinkAccessGuard accessGuard)
    : IRequestHandler<ListShortLinkSharesQuery, ShortLinkSharesResponse>
{
    public async Task<ShortLinkSharesResponse> Handle(
        ListShortLinkSharesQuery request,
        CancellationToken cancellationToken)
    {
        var user = await accessGuard.GetAuthorizedUserAsync(
            ShortenLinkPermissionCatalog.ShortLinksRead, cancellationToken);
        var link = ShortLinkFeatureSupport.GetRequired(
            await shortLinkService.GetDetailsAsync(request.Code, cancellationToken));
        await accessGuard.EnsureAccessAsync(
            link, user, ShortLinkShareAccess.Edit, true,
            "Only the owner or an admin can manage sharing.", cancellationToken);
        var response = new List<ShortLinkShareResponse>();
        foreach (var share in await shareRepository.ListByShortCodeAsync(request.Code, cancellationToken))
        {
            var target = await userRepository.FindByIdAsync(share.UserId, cancellationToken);
            response.Add(ShortLinkShareResponse.FromDomain(share, target));
        }
        return new ShortLinkSharesResponse(link.SharingMode.ToString(), response);
    }
}
