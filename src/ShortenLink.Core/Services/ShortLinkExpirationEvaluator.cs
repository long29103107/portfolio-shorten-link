using System.Globalization;
using System.Text;
using ShortenLink.Core.Abstractions;
using ShortenLink.Core.Contracts.Expiration;
using ShortenLink.Core.Domain;

namespace ShortenLink.Core.Services;

public sealed class ShortLinkExpirationEvaluator : IShortLinkExpirationEvaluator
{
    public const int DefaultLimit = 100;
    public const int MaxLimit = 500;

    public ShortLinkExpirationBatchResult Evaluate(
        IReadOnlyList<ShortLink> candidates,
        ShortLinkExpirationBatchRequest request)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(request);

        if (!ShortLinkTenantId.IsValid(request.TenantId))
        {
            throw new ArgumentException(
                $"Tenant identifier must be at most {ShortLinkTenantId.MaxLength} characters.",
                nameof(request));
        }

        var tenantId = ShortLinkTenantId.Normalize(request.TenantId);
        var policy = request.RetentionPolicy ?? ShortLinkRetentionPolicy.Immediate;
        var limit = Math.Clamp(request.Limit, 1, MaxLimit);
        if (!TryDecodeCursor(request.Cursor, out var beforeExpiresAt, out var beforeCode))
        {
            throw new ArgumentException("Expiration cursor is invalid.", nameof(request));
        }

        var ordered = candidates
            .Where(link => string.Equals(link.TenantId, tenantId, StringComparison.Ordinal))
            .OrderBy(link => link.ExpiresAt ?? DateTimeOffset.MaxValue)
            .ThenBy(link => link.Code, StringComparer.Ordinal)
            .Where(link => IsAfterCursor(link, beforeExpiresAt, beforeCode))
            .ToList();
        var page = ordered.Take(limit).ToList();
        var hasMore = ordered.Count > page.Count;
        var nextCursor = hasMore && page.Count > 0
            ? EncodeCursor(page[^1].ExpiresAt, page[^1].Code)
            : null;
        var items = page
            .Select(link => ShortLinkExpirationEvaluation.FromShortLink(link, request.EvaluatedAtUtc, policy))
            .ToList();

        return new ShortLinkExpirationBatchResult(items, nextCursor, hasMore);
    }

    private static bool IsAfterCursor(
        ShortLink link,
        DateTimeOffset? beforeExpiresAt,
        string? beforeCode)
    {
        if (beforeExpiresAt is null && string.IsNullOrWhiteSpace(beforeCode))
        {
            return true;
        }

        var expiresAt = link.ExpiresAt ?? DateTimeOffset.MaxValue;
        var cursorExpiresAt = beforeExpiresAt ?? DateTimeOffset.MaxValue;
        return expiresAt > cursorExpiresAt
            || (expiresAt == cursorExpiresAt
                && !string.IsNullOrWhiteSpace(beforeCode)
                && string.Compare(link.Code, beforeCode, StringComparison.Ordinal) > 0);
    }

    public static bool TryDecodeCursor(
        string? cursor,
        out DateTimeOffset? beforeExpiresAt,
        out string? beforeCode)
    {
        beforeExpiresAt = null;
        beforeCode = null;
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return true;
        }

        try
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split('|', 2);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[1]))
            {
                return false;
            }

            if (parts[0] == "max")
            {
                beforeCode = parts[1];
                return true;
            }

            if (!DateTimeOffset.TryParseExact(
                parts[0],
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed))
            {
                return false;
            }

            beforeExpiresAt = parsed;
            beforeCode = parts[1];
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string EncodeCursor(DateTimeOffset? expiresAt, string code) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(
            $"{(expiresAt?.ToString("O", CultureInfo.InvariantCulture) ?? "max")}|{code}"));
}
