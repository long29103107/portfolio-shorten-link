using ShortenLink.Core.Contracts.Expiration;
using ShortenLink.Core;

namespace ShortenLink.Core.Events;

/// <summary>
/// Versioned, secret-free expiration metadata for future cleanup and cache
/// consumers. It is emitted only by an opt-in evaluation hook; it never deletes
/// or mutates a short link itself.
/// </summary>
public sealed record ShortLinkExpirationEvent
{
    public const int CurrentVersion = 1;

    public ShortLinkExpirationEvent(
        string code,
        DateTimeOffset? expiresAtUtc,
        DateTimeOffset evaluatedAtUtc,
        ShortLinkExpirationOutcome outcome,
        string? tenantId = null,
        int version = CurrentVersion)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("A short code is required.", nameof(code));
        }

        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version));
        }

        Code = code;
        ExpiresAtUtc = expiresAtUtc;
        EvaluatedAtUtc = evaluatedAtUtc;
        Outcome = outcome;
        if (!ShortLinkTenantId.IsValid(tenantId))
        {
            throw new ArgumentException(
                $"Tenant identifier must be at most {ShortLinkTenantId.MaxLength} characters.",
                nameof(tenantId));
        }

        TenantId = ShortLinkTenantId.Normalize(tenantId);
        Version = version;
    }

    public int Version { get; }

    public string EventType => ShortLinkEventTypes.Expired;

    public string Code { get; }

    public string? TenantId { get; }

    public DateTimeOffset? ExpiresAtUtc { get; }

    public DateTimeOffset EvaluatedAtUtc { get; }

    public ShortLinkExpirationOutcome Outcome { get; }

    public bool CacheInvalidationRequired => Outcome == ShortLinkExpirationOutcome.Expired;

    public static ShortLinkExpirationEvent FromEvaluation(ShortLinkExpirationEvaluation evaluation) =>
        new(
            evaluation.Code,
            evaluation.ExpiresAtUtc,
            evaluation.EvaluatedAtUtc,
            evaluation.Outcome,
            evaluation.TenantId);
}
