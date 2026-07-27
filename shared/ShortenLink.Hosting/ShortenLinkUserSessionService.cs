using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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

public sealed class ShortenLinkUserSessionService(
    IOptions<ShortenLinkOptions> options,
    IShortenLinkSecurityUserRepository userRepository,
    IShortenLinkSecurityRoleRepository roleRepository,
    TimeProvider timeProvider) : IShortenLinkUserSessionService
{
    private const string BearerPrefix = "Bearer ";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<ShortenLinkUserSessionResult> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return ShortenLinkUserSessionResult.InvalidLogin();
        }

        var user = await userRepository
            .FindByUsernameAsync(username, cancellationToken)
            ;
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
        {
            return ShortenLinkUserSessionResult.Unauthorized();
        }

        var payload = ValidateToken(refreshToken, "refresh");
        if (payload is null)
        {
            return ShortenLinkUserSessionResult.Unauthorized();
        }

        var user = await userRepository.FindByIdAsync(payload.UserId, cancellationToken);
        if (user is null || !user.IsEnabled)
        {
            return ShortenLinkUserSessionResult.Unauthorized();
        }

        var issuedAtUtc = timeProvider.GetUtcNow();
        var principal = await CreatePrincipalAsync(user, issuedAtUtc, cancellationToken);
        return ShortenLinkUserSessionResult.Success(
            principal,
            CreateToken(user, issuedAtUtc, "access"),
            CreateToken(user, issuedAtUtc, "refresh"));
    }

    public async Task<ShortenLinkUserSessionResult> GetCurrentUserAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var token = ExtractBearerToken(httpContext);
        if (string.IsNullOrWhiteSpace(token))
        {
            return ShortenLinkUserSessionResult.Unauthorized();
        }

        var payload = ValidateToken(token, "access");
        if (payload is null)
        {
            return ShortenLinkUserSessionResult.Unauthorized();
        }

        var user = await userRepository
            .FindByIdAsync(payload.UserId, cancellationToken)
            ;
        if (user is null || !user.IsEnabled)
        {
            return ShortenLinkUserSessionResult.Unauthorized();
        }

        var principal = await CreatePrincipalAsync(
            user,
            DateTimeOffset.FromUnixTimeSeconds(payload.IssuedAtUnixSeconds),
            cancellationToken);

        return ShortenLinkUserSessionResult.Success(principal, token);
    }

    public async Task<ShortenLinkUserSessionPrincipal> CreatePrincipalAsync(
        ShortenLinkSecurityUser user,
        DateTimeOffset issuedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var permissions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var roleId in user.RoleIds)
        {
            var rolePermissions = new HashSet<string>(StringComparer.Ordinal);
            if (ShortenLinkSystemRoles.PermissionBundles.TryGetValue(roleId, out var systemPermissions))
            {
                foreach (var permission in systemPermissions)
                {
                    rolePermissions.Add(permission);
                }

            }
            else
            {
                var customRole = await roleRepository
                    .FindCustomRoleAsync(roleId, cancellationToken)
                    ;
                if (customRole is not { IsEnabled: true })
                {
                    continue;
                }

                foreach (var permission in customRole.Permissions)
                {
                    rolePermissions.Add(permission);
                }
            }

            await ApplyPermissionOverridesAsync(rolePermissions, roleId, cancellationToken);
            foreach (var permission in rolePermissions)
            {
                permissions.Add(permission);
            }
        }

        return new ShortenLinkUserSessionPrincipal(
            user.UserKey,
            user.Username,
            user.DisplayName,
            user.RoleIds,
            permissions.OrderBy(static permission => permission, StringComparer.Ordinal).ToList(),
            issuedAtUtc);
    }

    private async Task ApplyPermissionOverridesAsync(
        HashSet<string> permissions,
        string roleId,
        CancellationToken cancellationToken)
    {
        var overrides = await roleRepository
            .ListPermissionOverridesAsync(roleId, cancellationToken)
            ;
        foreach (var item in overrides)
        {
            if (item.IsAllowed) permissions.Add(item.Permission);
            else permissions.Remove(item.Permission);
        }
    }

    private string CreateToken(ShortenLinkSecurityUser user, DateTimeOffset issuedAtUtc, string kind)
    {
        var payload = new SessionTokenPayload(
            user.UserKey,
            user.Username,
            issuedAtUtc.ToUnixTimeSeconds(),
            kind,
            Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant());
        var payloadJson = JsonSerializer.Serialize(payload, SerializerOptions);
        var payloadSegment = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
        var signatureSegment = Base64UrlEncode(Sign(payloadSegment));

        return $"{payloadSegment}.{signatureSegment}";
    }

    private SessionTokenPayload? ValidateToken(string token, string expectedKind)
    {
        var parts = token.Split('.', 2);
        if (parts.Length != 2)
        {
            return null;
        }

        var expectedSignature = Sign(parts[0]);
        byte[] actualSignature;
        try
        {
            actualSignature = Base64UrlDecode(parts[1]);
        }
        catch (FormatException)
        {
            return null;
        }

        if (!CryptographicOperations.FixedTimeEquals(actualSignature, expectedSignature))
        {
            return null;
        }

        SessionTokenPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<SessionTokenPayload>(
                Base64UrlDecodeToString(parts[0]),
                SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }

        if (payload is null
            || string.IsNullOrWhiteSpace(payload.UserId)
            || !string.Equals(payload.Kind, expectedKind, StringComparison.Ordinal))
        {
            return null;
        }

        var ttlMinutes = expectedKind == "refresh"
            ? Math.Max(options.Value.Security.RefreshTokenTtlMinutes, 1)
            : Math.Max(options.Value.Security.SessionTokenTtlMinutes, 1);
        var expiresAtUtc = DateTimeOffset
            .FromUnixTimeSeconds(payload.IssuedAtUnixSeconds)
            .AddMinutes(ttlMinutes);

        return timeProvider.GetUtcNow() <= expiresAtUtc ? payload : null;
    }

    private byte[] Sign(string payloadSegment)
    {
        using var hmac = new HMACSHA256(GetSigningKey());
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadSegment));
    }

    private byte[] GetSigningKey()
    {
        var security = options.Value.Security;
        var configuredKey = security.SessionSigningKey;
        var keyMaterial = string.IsNullOrWhiteSpace(configuredKey)
            ? string.Join('|', security.ApiKeys.Select(static apiKey => apiKey.Key).Where(static key => !string.IsNullOrWhiteSpace(key)))
            : configuredKey;

        if (string.IsNullOrWhiteSpace(keyMaterial))
        {
            keyMaterial = $"{security.HeaderName}|shorten-link-local-session";
        }

        return SHA256.HashData(Encoding.UTF8.GetBytes(keyMaterial));
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

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }

    private static string Base64UrlDecodeToString(string value) =>
        Encoding.UTF8.GetString(Base64UrlDecode(value));

    private sealed record SessionTokenPayload(
        string UserId,
        string Username,
        long IssuedAtUnixSeconds,
        string Kind,
        string Nonce);
}
