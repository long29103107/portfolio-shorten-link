using Microsoft.AspNetCore.Http;
using ShortenLink.Application.Abstractions;
using ShortenLink.Core.Exceptions;

namespace ShortenLink.Hosting;

internal sealed class HttpCurrentRequestContext(
    IHttpContextAccessor httpContextAccessor,
    IShortenLinkAuthorizationService authorizationService,
    IShortenLinkUserSessionService userSessionService) : ICurrentRequestContext
{
    public async Task EnsureAuthorizedAsync(
        string permission,
        CancellationToken cancellationToken = default)
    {
        var result = await AuthorizeResultAsync(permission, cancellationToken);
        if (!result.Succeeded)
        {
            throw ToException(result);
        }
    }

    public async Task<CurrentRequestActor> AuthorizeAsync(
        string permission,
        CancellationToken cancellationToken = default)
    {
        var result = await AuthorizeResultAsync(permission, cancellationToken);
        if (!result.Succeeded)
        {
            throw ToException(result);
        }

        return new CurrentRequestActor(result.UserId, result.IsAdmin, result.ActorId);
    }

    public async Task<CurrentUser?> GetCurrentUserAsync(
        CancellationToken cancellationToken = default)
    {
        var session = await userSessionService
            .GetCurrentUserAsync(GetHttpContext(), cancellationToken);

        return session.Succeeded && session.Principal is not null
            ? new CurrentUser(
                session.Principal.UserId,
                session.Principal.Username,
                session.Principal.DisplayName,
                session.Principal.Roles,
                session.Principal.Permissions)
            : null;
    }

    private async Task<ShortenLinkAuthorizationResult> AuthorizeResultAsync(
        string permission,
        CancellationToken cancellationToken) =>
        await authorizationService.AuthorizeAsync(
            GetHttpContext(),
            permission,
            cancellationToken);

    private static Exception ToException(ShortenLinkAuthorizationResult result) =>
        result.IsAuthenticated
            ? new ForbiddenException(
                result.ErrorCode ?? ErrorCodes.Forbidden,
                result.ErrorMessage ?? "The request is not authorized.")
            : new AuthenticationRequiredException(
                result.ErrorCode ?? ErrorCodes.Unauthorized,
                result.ErrorMessage ?? "A valid credential is required.");

    private HttpContext GetHttpContext() =>
        httpContextAccessor.HttpContext
        ?? throw new InvalidOperationException("The HTTP request context is unavailable.");
}
