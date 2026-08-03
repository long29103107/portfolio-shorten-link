using ShortenLink.Core.Domain;
using ShortenLink.Core.Services;

namespace ShortenLink.Core.Contracts.Expiration;

public enum ShortLinkExpirationOutcome
{
    Skipped,
    Retained,
    Expired
}

public sealed record ShortLinkRetentionPolicy
{
    public static ShortLinkRetentionPolicy Immediate { get; } = new(TimeSpan.Zero);

    public ShortLinkRetentionPolicy(TimeSpan retainExpiredFor)
    {
        if (retainExpiredFor < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(retainExpiredFor),
                retainExpiredFor,
                "Retention duration cannot be negative.");
        }

        RetainExpiredFor = retainExpiredFor;
    }

    public TimeSpan RetainExpiredFor { get; }
}

public sealed record ShortLinkExpirationBatchRequest(
    DateTimeOffset EvaluatedAtUtc,
    string? TenantId = null,
    string? Cursor = null,
    int Limit = ShortLinkExpirationEvaluator.DefaultLimit,
    ShortLinkRetentionPolicy? RetentionPolicy = null);

public sealed record ShortLinkExpirationEvaluation(
    string Code,
    string? TenantId,
    DateTimeOffset? ExpiresAtUtc,
    DateTimeOffset EvaluatedAtUtc,
    ShortLinkExpirationOutcome Outcome,
    string Reason,
    bool CacheInvalidationRequired)
{
    public static ShortLinkExpirationEvaluation FromShortLink(
        ShortLink shortLink,
        DateTimeOffset evaluatedAtUtc,
        ShortLinkRetentionPolicy retentionPolicy)
    {
        ArgumentNullException.ThrowIfNull(shortLink);
        ArgumentNullException.ThrowIfNull(retentionPolicy);

        if (!shortLink.IsActive)
        {
            return new(
                shortLink.Code,
                shortLink.TenantId,
                shortLink.ExpiresAt,
                evaluatedAtUtc,
                ShortLinkExpirationOutcome.Skipped,
                "inactive",
                false);
        }

        if (shortLink.ExpiresAt is null)
        {
            return new(
                shortLink.Code,
                shortLink.TenantId,
                null,
                evaluatedAtUtc,
                ShortLinkExpirationOutcome.Skipped,
                "no_expiration",
                false);
        }

        if (shortLink.ExpiresAt > evaluatedAtUtc)
        {
            return new(
                shortLink.Code,
                shortLink.TenantId,
                shortLink.ExpiresAt,
                evaluatedAtUtc,
                ShortLinkExpirationOutcome.Retained,
                "not_expired",
                false);
        }

        var retentionCutoff = evaluatedAtUtc - retentionPolicy.RetainExpiredFor;
        var isRetained = shortLink.ExpiresAt > retentionCutoff;
        return new(
            shortLink.Code,
            shortLink.TenantId,
            shortLink.ExpiresAt,
            evaluatedAtUtc,
            isRetained ? ShortLinkExpirationOutcome.Retained : ShortLinkExpirationOutcome.Expired,
            isRetained ? "retention_window" : "retention_elapsed",
            !isRetained);
    }
}

public sealed record ShortLinkExpirationBatchResult(
    IReadOnlyList<ShortLinkExpirationEvaluation> Items,
    string? NextCursor,
    bool HasMore);
