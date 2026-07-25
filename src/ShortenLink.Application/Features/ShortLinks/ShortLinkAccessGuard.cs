using ShortenLink.Application.Abstractions;
using ShortenLink.Core.Security;

namespace ShortenLink.Application.Features.ShortLinks;

public sealed class ShortLinkAccessGuard(
    ICurrentRequestContext requestContext,
    IShortLinkShareRepository shareRepository)
{
    internal Task<CurrentRequestActor> GetAuthorizedUserAsync(
        string permission,
        CancellationToken cancellationToken) =>
        requestContext.AuthorizeAsync(permission, cancellationToken);

    internal async Task<ShortLinkAccessScope> CreateScopeAsync(
        CurrentRequestActor user,
        CancellationToken cancellationToken)
    {
        var sharedAccess = user.IsAdmin || string.IsNullOrWhiteSpace(user.UserId)
            ? new Dictionary<string, ShortLinkShareAccess>(StringComparer.Ordinal)
            : await shareRepository.ListSharedAccessAsync(user.UserId, cancellationToken);
        return new ShortLinkAccessScope(user.UserId, user.IsAdmin, sharedAccess);
    }

    internal async Task EnsureAccessAsync(
        ShortLink shortLink,
        CurrentRequestActor user,
        ShortLinkShareAccess requiredAccess,
        bool ownerOnly,
        string message,
        CancellationToken cancellationToken)
    {
        if (user.IsAdmin
            || string.Equals(shortLink.CreatedByUserId, user.UserId, StringComparison.Ordinal))
            return;
        if (!ownerOnly && !string.IsNullOrWhiteSpace(user.UserId))
        {
            var share = await shareRepository.FindAsync(shortLink.Code, user.UserId, cancellationToken);
            if (share is not null && share.Access >= requiredAccess)
                return;
        }
        throw new ForbiddenException(ErrorCodes.Forbidden, message);
    }

    internal static string GetAccessLevel(ShortLink shortLink, ShortLinkAccessScope scope)
    {
        if (scope.IsAdmin)
            return "Admin";
        if (string.Equals(shortLink.CreatedByUserId, scope.UserId, StringComparison.Ordinal))
            return "Owner";
        return scope.SharedAccess.TryGetValue(shortLink.Code, out var access)
            ? access.ToString()
            : "None";
    }

}
