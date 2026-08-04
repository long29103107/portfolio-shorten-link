using ShortenLink.Application.Features.Audit;
using ShortenLink.Core.Security;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Features.ShortLinks.Shares;

public sealed record SetShortLinkSharingModeCommand(
    string Code,
    string Mode) : IRequest<string>;

internal sealed class SetShortLinkSharingModeCommandHandler(
    IShortLinkService shortLinkService,
    IShortLinkRepository shortLinkRepository,
    ShortLinkAccessGuard accessGuard,
    ShortLinkAuditWriter auditWriter)
    : IRequestHandler<SetShortLinkSharingModeCommand, string>
{
    public async Task<string> Handle(
        SetShortLinkSharingModeCommand request,
        CancellationToken cancellationToken)
    {
        var user = await accessGuard.GetAuthorizedUserAsync(
            ShortenLinkPermissionCatalog.ShortLinksUpdate, cancellationToken);
        var link = ShortLinkFeatureSupport.GetRequired(
            await shortLinkService.GetDetailsAsync(request.Code, cancellationToken));
        await accessGuard.EnsureAccessAsync(
            link, user, ShortLinkShareAccess.Edit, true,
            "Only the owner or an admin can manage sharing.", cancellationToken);

        var mode = Enum.Parse<ShortLinkSharingMode>(request.Mode, true);

        if (link.SharingMode != mode)
        {
            link.SetSharingMode(mode);
            await shortLinkRepository.UpdateAsync(link, cancellationToken);
            await auditWriter.RecordAsync(
                user,
                ShortLinkAuditActions.ShareUpdated,
                link.Code,
                link.CreatedByUserId,
                detail: mode.ToString(),
                cancellationToken: cancellationToken);
        }

        return mode.ToString();
    }
}
