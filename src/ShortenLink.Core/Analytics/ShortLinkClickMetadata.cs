using System.Security.Cryptography;
using System.Text;

namespace ShortenLink.Core.Analytics;

public sealed record ShortLinkClickMetadata(
    string? Device,
    string? Browser,
    string? OperatingSystem,
    string? CountryCode,
    string? VisitorKeyHash)
{
    public static ShortLinkClickMetadata FromRequest(
        string? remoteIpAddress,
        string? userAgent,
        string? countryCode) =>
        new(
            ShortLinkClickMetadataParser.DetectDevice(userAgent),
            ShortLinkClickMetadataParser.DetectBrowser(userAgent),
            ShortLinkClickMetadataParser.DetectOperatingSystem(userAgent),
            ShortLinkClickMetadataParser.NormalizeCountryCode(countryCode),
            ShortLinkClickMetadataParser.CreateVisitorKeyHash(remoteIpAddress, userAgent));
}

public static class ShortLinkClickMetadataParser
{
    public static string? DetectDevice(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return null;
        }

        if (ContainsAny(userAgent, "bot", "crawler", "spider", "slurp", "headless"))
        {
            return "Bot";
        }

        if (ContainsAny(userAgent, "iPad", "Tablet", "Android")
            && !userAgent.Contains("Mobile", StringComparison.OrdinalIgnoreCase))
        {
            return "Tablet";
        }

        if (ContainsAny(userAgent, "Mobile", "iPhone", "iPod", "Windows Phone"))
        {
            return "Mobile";
        }

        return "Desktop";
    }

    public static string? DetectBrowser(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return null;
        }

        if (ContainsAny(userAgent, "bot", "crawler", "spider", "slurp", "headless"))
        {
            return "Bot";
        }

        if (userAgent.Contains("Edg/", StringComparison.OrdinalIgnoreCase))
        {
            return "Edge";
        }

        if (userAgent.Contains("OPR/", StringComparison.OrdinalIgnoreCase)
            || userAgent.Contains("Opera", StringComparison.OrdinalIgnoreCase))
        {
            return "Opera";
        }

        if (userAgent.Contains("SamsungBrowser/", StringComparison.OrdinalIgnoreCase))
        {
            return "Samsung Internet";
        }

        if (userAgent.Contains("Firefox/", StringComparison.OrdinalIgnoreCase))
        {
            return "Firefox";
        }

        if (userAgent.Contains("Chrome/", StringComparison.OrdinalIgnoreCase)
            || userAgent.Contains("CriOS/", StringComparison.OrdinalIgnoreCase))
        {
            return "Chrome";
        }

        if (userAgent.Contains("Safari/", StringComparison.OrdinalIgnoreCase))
        {
            return "Safari";
        }

        if (userAgent.Contains("Trident/", StringComparison.OrdinalIgnoreCase)
            || userAgent.Contains("MSIE ", StringComparison.OrdinalIgnoreCase))
        {
            return "Internet Explorer";
        }

        return "Other";
    }

    public static string? DetectOperatingSystem(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return null;
        }

        if (userAgent.Contains("Windows", StringComparison.OrdinalIgnoreCase))
        {
            return "Windows";
        }

        if (userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase)
            || userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase)
            || userAgent.Contains("iPod", StringComparison.OrdinalIgnoreCase))
        {
            return "iOS";
        }

        if (userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase))
        {
            return "Android";
        }

        if (userAgent.Contains("Mac OS X", StringComparison.OrdinalIgnoreCase)
            || userAgent.Contains("Macintosh", StringComparison.OrdinalIgnoreCase))
        {
            return "macOS";
        }

        if (userAgent.Contains("CrOS", StringComparison.OrdinalIgnoreCase))
        {
            return "Chrome OS";
        }

        if (userAgent.Contains("Linux", StringComparison.OrdinalIgnoreCase))
        {
            return "Linux";
        }

        return "Other";
    }

    public static string? NormalizeCountryCode(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return null;
        }

        var normalized = countryCode.Trim().ToUpperInvariant();
        return normalized.Length is >= 2 and <= 3
            && normalized.All(static character => char.IsLetterOrDigit(character))
            ? normalized
            : null;
    }

    public static string? CreateVisitorKeyHash(
        string? remoteIpAddress,
        string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(remoteIpAddress)
            && string.IsNullOrWhiteSpace(userAgent))
        {
            return null;
        }

        var fingerprint = string.Join(
            '\n',
            remoteIpAddress?.Trim() ?? string.Empty,
            userAgent?.Trim() ?? string.Empty);
        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint)))
            .ToLowerInvariant();
    }

    private static bool ContainsAny(string value, params string[] candidates) =>
        candidates.Any(candidate => value.Contains(candidate, StringComparison.OrdinalIgnoreCase));
}
