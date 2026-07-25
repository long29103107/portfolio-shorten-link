using ShortenLink.Core.Security;
using ShortenLink.Application.Features.Audit;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Features.ShortLinks.Status;

public sealed record DeactivateShortLinkCommand(
    string Code) : IRequest<ShortLinkDeactivatedResponse>;

internal sealed class DeactivateShortLinkCommandHandler(
    IShortLinkService shortLinkService,
    ShortLinkAccessGuard accessGuard,
    ShortLinkAuditWriter auditWriter)
    : IRequestHandler<DeactivateShortLinkCommand, ShortLinkDeactivatedResponse>
{
    public async Task<ShortLinkDeactivatedResponse> Handle(
        DeactivateShortLinkCommand request,
        CancellationToken cancellationToken)
    {
        var user = await accessGuard.GetAuthorizedUserAsync(
            ShortenLinkPermissionCatalog.ShortLinksStatus, cancellationToken);
        var existing = ShortLinkFeatureSupport.GetRequired(
            await shortLinkService.GetDetailsAsync(request.Code, cancellationToken));
        await accessGuard.EnsureAccessAsync(
            existing, user, ShortLinkShareAccess.Edit, false,
            "Edit access is required for this short link.", cancellationToken);
        ShortLinkFeatureSupport.EnsureSucceeded(
            await shortLinkService.DeactivateAsync(request.Code, cancellationToken));
        await auditWriter.RecordAsync(
            user,
            ShortLinkAuditActions.Deactivated,
            existing.Code,
            existing.CreatedByUserId,
            cancellationToken: cancellationToken);
        return new ShortLinkDeactivatedResponse(request.Code, false);
    }
}
