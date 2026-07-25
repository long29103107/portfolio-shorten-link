using ShortenLink.Application.Abstractions;
using ShortenLink.AspNetCore;

namespace ShortenLink.Api;

internal sealed class SecuritySessionServiceAdapter(
    IShortenLinkUserSessionService sessionService) : ISecuritySessionService
{
    public async Task<SecuritySessionResult> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default) =>
        Map(await sessionService.LoginAsync(email, password, cancellationToken));

    public async Task<SecuritySessionResult> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default) =>
        Map(await sessionService.RefreshAsync(refreshToken, cancellationToken));

    private static SecuritySessionResult Map(ShortenLinkUserSessionResult result)
    {
        var principal = result.Principal;
        return new SecuritySessionResult(
            result.Succeeded,
            principal is null
                ? null
                : new CurrentUser(
                    principal.UserId,
                    principal.Username,
                    principal.DisplayName,
                    principal.Roles,
                    principal.Permissions),
            result.Token,
            result.RefreshToken,
            principal?.IssuedAtUtc,
            result.ErrorCode,
            result.ErrorMessage);
    }
}
