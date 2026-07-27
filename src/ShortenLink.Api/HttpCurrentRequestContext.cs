using ShortenLink.Application.Abstractions;
using ShortenLink.Hosting;

namespace ShortenLink.Api;

internal sealed class HttpCurrentRequestContext(
    IHttpContextAccessor httpContextAccessor,
    IShortenLinkAuthorizationService authorizationService,
    IShortenLinkUserSessionService userSessionService) : ICurrentRequestContext
{
    public async Task EnsureAuthorizedAsync(
        string permission,
        CancellationToken cancellationToken = default)
    {
        var httpContext = GetHttpContext();
        var result = await authorizationService
            .AuthorizeAsync(httpContext, permission, cancellationToken)
            ;

        if (!result.Succeeded)
        {
            throw result.IsAuthenticated
                ? new ForbiddenException(
                    result.ErrorCode ?? ErrorCodes.Forbidden,
                    result.ErrorMessage ?? "The request is not authorized.")
                : new AuthenticationRequiredException(
                    result.ErrorCode ?? ErrorCodes.Unauthorized,
                    result.ErrorMessage ?? "A valid credential is required.");
        }
    }

    public async Task<CurrentRequestActor> AuthorizeAsync(
        string permission,
        CancellationToken cancellationToken = default)
    {
        var result = await authorizationService.AuthorizeAsync(
            GetHttpContext(), permission, cancellationToken);
        if (!result.Succeeded)
        {
            throw result.IsAuthenticated
                ? new ForbiddenException(
                    result.ErrorCode ?? ErrorCodes.Forbidden,
                    result.ErrorMessage ?? "The request is not authorized.")
                : new AuthenticationRequiredException(
                    result.ErrorCode ?? ErrorCodes.Unauthorized,
                    result.ErrorMessage ?? "A valid credential is required.");
        }
        return new CurrentRequestActor(result.UserId, result.IsAdmin, result.ActorId);
    }

    public async Task<CurrentUser?> GetCurrentUserAsync(
        CancellationToken cancellationToken = default)
    {
        var session = await userSessionService
            .GetCurrentUserAsync(GetHttpContext(), cancellationToken)
            ;

        return session.Succeeded && session.Principal is not null
            ? new CurrentUser(
                session.Principal.UserId,
                session.Principal.Username,
                session.Principal.DisplayName,
                session.Principal.Roles,
                session.Principal.Permissions)
            : null;
    }

    private HttpContext GetHttpContext() =>
        httpContextAccessor.HttpContext
        ?? throw new InvalidOperationException("The HTTP request context is unavailable.");
}
