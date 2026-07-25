using ShortenLink.Core.Security;
using ShortenLink.Application.Features.Audit;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Features.ShortLinks.Delete;

public sealed record DeleteShortLinkCommand(
    string Code) : IRequest<ShortLinkDeletedResponse>;

internal sealed class DeleteShortLinkCommandHandler(
    IShortLinkService shortLinkService,
    IShortLinkShareRepository shareRepository,
    ShortLinkAccessGuard accessGuard,
    ShortLinkAuditWriter auditWriter)
    : IRequestHandler<DeleteShortLinkCommand, ShortLinkDeletedResponse>
{
    public async Task<ShortLinkDeletedResponse> Handle(
        DeleteShortLinkCommand request,
        CancellationToken cancellationToken)
    {
        var user = await accessGuard.GetAuthorizedUserAsync(
            ShortenLinkPermissionCatalog.ShortLinksDelete, cancellationToken);
        var existing = ShortLinkFeatureSupport.GetRequired(
            await shortLinkService.GetDetailsAsync(request.Code, cancellationToken));
        await accessGuard.EnsureAccessAsync(
            existing, user, ShortLinkShareAccess.Edit, true,
            "Only the owner or an admin can delete this short link.", cancellationToken);
        ShortLinkFeatureSupport.EnsureSucceeded(
            await shortLinkService.DeleteAsync(request.Code, cancellationToken));
        await shareRepository.DeleteByShortCodeAsync(request.Code, cancellationToken);
        await auditWriter.RecordAsync(
            user,
            ShortLinkAuditActions.Deleted,
            existing.Code,
            existing.CreatedByUserId,
            cancellationToken: cancellationToken);
        return new ShortLinkDeletedResponse(request.Code);
    }
}
