using ShortenLink.Core.Security;
using ShortenLink.Application.Features.Audit;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Features.ShortLinks.Shares;

public sealed record DeleteShortLinkShareCommand(
    string Code,
    string UserId) : IRequest<bool>;

internal sealed class DeleteShortLinkShareCommandHandler(
    IShortLinkService shortLinkService,
    IShortLinkShareRepository shareRepository,
    ShortLinkAccessGuard accessGuard,
    ShortLinkAuditWriter auditWriter)
    : IRequestHandler<DeleteShortLinkShareCommand, bool>
{
    public async Task<bool> Handle(
        DeleteShortLinkShareCommand request,
        CancellationToken cancellationToken)
    {
        var user = await accessGuard.GetAuthorizedUserAsync(
            ShortenLinkPermissionCatalog.ShortLinksUpdate, cancellationToken);
        var link = ShortLinkFeatureSupport.GetRequired(
            await shortLinkService.GetDetailsAsync(request.Code, cancellationToken));
        await accessGuard.EnsureAccessAsync(
            link, user, ShortLinkShareAccess.Edit, true,
            "Only the owner or an admin can manage sharing.", cancellationToken);
        if (!await shareRepository.DeleteAsync(request.Code, request.UserId, cancellationToken))
            throw new NotFoundException(ErrorCodes.NotFound, "Share was not found.");
        await auditWriter.RecordAsync(
            user,
            ShortLinkAuditActions.ShareRevoked,
            link.Code,
            link.CreatedByUserId,
            request.UserId,
            cancellationToken: cancellationToken);
        return true;
    }
}
