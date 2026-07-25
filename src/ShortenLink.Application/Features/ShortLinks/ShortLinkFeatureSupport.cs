using System.Globalization;
using System.Text;
using ShortenLink.Core.Services;

namespace ShortenLink.Application.Features.ShortLinks;

internal static class ShortLinkFeatureSupport
{
    internal static ShortLink GetRequired(ShortLinkDetailsResult result)
    {
        if (result.Succeeded && result.ShortLink is not null)
            return result.ShortLink;
        throw CreateException(result.ErrorCode, result.ErrorMessage);
    }

    internal static void EnsureSucceeded(DeactivateShortLinkResult result)
    {
        if (!result.Succeeded)
            throw CreateException(result.ErrorCode, result.ErrorMessage);
    }

    internal static ShortenLinkException CreateException(string? code, string? message) =>
        code switch
        {
            ShortLinkErrorCodes.InvalidUrl or ShortLinkErrorCodes.InvalidExpiration or ShortLinkErrorCodes.InvalidCode =>
                new RequestValidationException(code, message ?? "The request is invalid."),
            ShortLinkErrorCodes.NotFound => new NotFoundException(code, message ?? "Short link was not found."),
            ShortLinkErrorCodes.Expired or ShortLinkErrorCodes.Inactive =>
                new ResourceGoneException(code, message ?? "Short link is unavailable."),
            _ => new BusinessRuleException(code ?? ErrorCodes.OperationFailed, message ?? "The operation failed.")
        };

    internal static string BuildShortUrl(string baseUrl, string code) =>
        new Uri(new Uri(baseUrl, UriKind.Absolute), code).AbsoluteUri;

    internal static bool TryDecodeCursor(
        string? cursor,
        out DateTimeOffset? beforeCreatedAt,
        out string? beforeCode)
    {
        beforeCreatedAt = null;
        beforeCode = null;
        if (string.IsNullOrWhiteSpace(cursor))
            return true;
        try
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split('|', 2);
            if (parts.Length == 2
                && !string.IsNullOrWhiteSpace(parts[1])
                && DateTimeOffset.TryParseExact(
                    parts[0], "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            {
                beforeCreatedAt = parsed;
                beforeCode = parts[1];
                return true;
            }
        }
        catch (FormatException)
        {
        }
        return false;
    }

    internal static string EncodeCursor(DateTimeOffset createdAt, string code) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(
            $"{createdAt.ToString("O", CultureInfo.InvariantCulture)}|{code}"));
}
