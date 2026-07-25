using ShortenLink.Core.Security;
using ShortenLink.Application.Features.Audit;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Features.ShortLinks.Shares;

public sealed record UpsertShortLinkShareCommand(
    string Code,
    string Username,
    string Access) : IRequest<ShortLinkShareResponse>;

internal sealed class UpsertShortLinkShareCommandHandler(
    IShortLinkService shortLinkService,
    IShortLinkShareRepository shareRepository,
    IShortenLinkSecurityUserRepository userRepository,
    ShortLinkAccessGuard accessGuard,
    TimeProvider timeProvider,
    ShortLinkAuditWriter auditWriter)
    : IRequestHandler<UpsertShortLinkShareCommand, ShortLinkShareResponse>
{
    public async Task<ShortLinkShareResponse> Handle(
        UpsertShortLinkShareCommand request,
        CancellationToken cancellationToken)
    {
        var user = await accessGuard.GetAuthorizedUserAsync(
            ShortenLinkPermissionCatalog.ShortLinksUpdate, cancellationToken);
        var link = ShortLinkFeatureSupport.GetRequired(
            await shortLinkService.GetDetailsAsync(request.Code, cancellationToken));
        await accessGuard.EnsureAccessAsync(
            link, user, ShortLinkShareAccess.Edit, true,
            "Only the owner or an admin can manage sharing.", cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Username)
            || !Enum.TryParse<ShortLinkShareAccess>(request.Access, true, out var access))
            throw new RequestValidationException(ErrorCodes.InvalidShare, "Choose a user and View or Edit access.");
        var target = await userRepository.FindByUsernameAsync(request.Username, cancellationToken);
        if (target is not { IsEnabled: true })
            throw new RequestValidationException(ErrorCodes.InvalidShareUser, "The selected user is unavailable.");
        if (string.Equals(link.CreatedByUserId, target.UserKey, StringComparison.Ordinal))
            throw new RequestValidationException(ErrorCodes.InvalidShare, "The owner already has full access.");

        var existingShare = await shareRepository.FindAsync(
            request.Code,
            target.UserKey,
            cancellationToken);
        var share = new ShortLinkShare(
            request.Code,
            target.UserKey,
            access,
            user.UserId ?? "system",
            timeProvider.GetUtcNow());
        await shareRepository.AddOrUpdateAsync(share, cancellationToken);
        await auditWriter.RecordAsync(
            user,
            existingShare is null
                ? ShortLinkAuditActions.ShareGranted
                : ShortLinkAuditActions.ShareUpdated,
            link.Code,
            link.CreatedByUserId,
            target.UserKey,
            share.Access.ToString(),
            cancellationToken);
        return ShortLinkShareResponse.FromDomain(share, target);
    }
}
