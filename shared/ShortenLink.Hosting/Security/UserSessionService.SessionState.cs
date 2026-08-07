using Microsoft.AspNetCore.Http;

namespace ShortenLink.Hosting;

public sealed partial class UserSessionService
{
    private const string BearerPrefix = "Bearer ";

    public async Task<ShortenLinkUserSessionResult> GetCurrentUserAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var token = ExtractBearerToken(httpContext);
        if (string.IsNullOrWhiteSpace(token))
            return ShortenLinkUserSessionResult.Unauthorized();

        var payload = ValidateToken(token, "access");
        if (payload is null)
            return ShortenLinkUserSessionResult.Unauthorized();

        var user = await userRepository.FindByIdAsync(payload.UserId, cancellationToken);
        if (user is null || !user.IsEnabled)
            return ShortenLinkUserSessionResult.Unauthorized();

        var principal = await CreatePrincipalAsync(
            user,
            DateTimeOffset.FromUnixTimeSeconds(payload.IssuedAtUnixSeconds),
            cancellationToken);
        return ShortenLinkUserSessionResult.Success(principal, token);
    }

    private static string? ExtractBearerToken(HttpContext httpContext)
    {
        var authorization = httpContext.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authorization)
            || !authorization.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = authorization[BearerPrefix.Length..].Trim();
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }
}
