using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShortenLink.Core.Security;

namespace ShortenLink.Hosting;

public sealed partial class UserSessionService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

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
            return null;

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
            return null;

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
            ? string.Join('|', security.ApiKeys.Select(static apiKey => apiKey.Key)
                .Where(static key => !string.IsNullOrWhiteSpace(key)))
            : configuredKey;

        if (string.IsNullOrWhiteSpace(keyMaterial))
            keyMaterial = $"{security.HeaderName}|shorten-link-local-session";

        return SHA256.HashData(Encoding.UTF8.GetBytes(keyMaterial));
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
