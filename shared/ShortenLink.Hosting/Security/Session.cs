using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using ShortenLink.Core.Security;

namespace ShortenLink.Hosting;

public interface IShortenLinkUserSessionService
{
    Task<ShortenLinkUserSessionResult> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);

    Task<ShortenLinkUserSessionResult> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    Task<ShortenLinkUserSessionResult> GetCurrentUserAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken = default);

    Task<ShortenLinkUserSessionPrincipal> CreatePrincipalAsync(
        ShortenLinkSecurityUser user,
        DateTimeOffset issuedAtUtc,
        CancellationToken cancellationToken = default);
}

public sealed record ShortenLinkUserSessionPrincipal(
    string UserId,
    string Username,
    string DisplayName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    DateTimeOffset IssuedAtUtc);

public sealed record ShortenLinkUserSessionResult(
    bool Succeeded,
    bool IsAuthenticated,
    ShortenLinkUserSessionPrincipal? Principal,
    string? Token,
    string? RefreshToken,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static ShortenLinkUserSessionResult Success(
        ShortenLinkUserSessionPrincipal principal,
        string? token,
        string? refreshToken = null) =>
        new(true, true, principal, token, refreshToken, null, null);

    public static ShortenLinkUserSessionResult Unauthorized() =>
        new(false, false, null, null, null, "unauthorized", "A valid user session is required.");

    public static ShortenLinkUserSessionResult InvalidLogin() =>
        new(false, false, null, null, null, "invalid_login", "Username or password is invalid.");
}

public sealed partial class UserSessionService(
    IOptions<ShortenLinkOptions> options,
    IShortenLinkSecurityUserRepository userRepository,
    IShortenLinkSecurityRoleRepository roleRepository,
    TimeProvider timeProvider) : IShortenLinkUserSessionService
{
    public async Task<ShortenLinkUserSessionResult> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return ShortenLinkUserSessionResult.InvalidLogin();

        var user = await userRepository.FindByUsernameAsync(username, cancellationToken);
        if (user is null
            || !user.IsEnabled
            || !ShortenLinkSecurityCredentialHasher.VerifyPassword(password, user.PasswordHash))
        {
            return ShortenLinkUserSessionResult.InvalidLogin();
        }

        var issuedAtUtc = timeProvider.GetUtcNow();
        var principal = await CreatePrincipalAsync(user, issuedAtUtc, cancellationToken);
        return ShortenLinkUserSessionResult.Success(
            principal,
            CreateToken(user, issuedAtUtc, "access"),
            CreateToken(user, issuedAtUtc, "refresh"));
    }

    public async Task<ShortenLinkUserSessionResult> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return ShortenLinkUserSessionResult.Unauthorized();

        var payload = ValidateToken(refreshToken, "refresh");
        if (payload is null)
            return ShortenLinkUserSessionResult.Unauthorized();

        var user = await userRepository.FindByIdAsync(payload.UserId, cancellationToken);
        if (user is null || !user.IsEnabled)
            return ShortenLinkUserSessionResult.Unauthorized();

        var issuedAtUtc = timeProvider.GetUtcNow();
        var principal = await CreatePrincipalAsync(user, issuedAtUtc, cancellationToken);
        return ShortenLinkUserSessionResult.Success(
            principal,
            CreateToken(user, issuedAtUtc, "access"),
            CreateToken(user, issuedAtUtc, "refresh"));
    }
}
