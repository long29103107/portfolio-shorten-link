using ShortenLink.Core.Security;
using ShortenLink.Application.Features.Audit;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Features.ShortLinks.Update;

public sealed record UpdateShortLinkCommand(
    string Code,
    string OriginalUrl,
    DateTimeOffset? ExpiresAt,
    string BaseUrl,
    DateTimeOffset? ActiveFromUtc = null,
    int? MaxClicks = null) : IRequest<ShortLinkAdminListItemResponse>;

internal sealed class UpdateShortLinkCommandHandler(
    IShortLinkService shortLinkService,
    ShortLinkAccessGuard accessGuard,
    ShortLinkAuditWriter auditWriter)
    : IRequestHandler<UpdateShortLinkCommand, ShortLinkAdminListItemResponse>
{
    public async Task<ShortLinkAdminListItemResponse> Handle(
        UpdateShortLinkCommand request,
        CancellationToken cancellationToken)
    {
        var user = await accessGuard.GetAuthorizedUserAsync(
            ShortenLinkPermissionCatalog.ShortLinksUpdate, cancellationToken);
        var existing = ShortLinkFeatureSupport.GetRequired(
            await shortLinkService.GetDetailsAsync(request.Code, cancellationToken));
        await accessGuard.EnsureAccessAsync(
            existing, user, ShortLinkShareAccess.Edit, false,
            "Edit access is required for this short link.", cancellationToken);
        var updated = ShortLinkFeatureSupport.GetRequired(
            await shortLinkService.UpdateAsync(
                request.Code,
                new UpdateShortLinkRequest(request.OriginalUrl, request.ExpiresAt, request.ActiveFromUtc, request.MaxClicks),
                cancellationToken));
        await auditWriter.RecordAsync(
            user,
            ShortLinkAuditActions.Updated,
            updated.Code,
            updated.CreatedByUserId,
            cancellationToken: cancellationToken);
        return ShortLinkAdminListItemResponse.FromDomain(
            updated,
            ShortLinkFeatureSupport.BuildShortUrl(request.BaseUrl, updated.Code));
    }
}
