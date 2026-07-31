using ShortenLink.Core.Domain;

namespace ShortenLink.Core.Events;

/// <summary>
/// Versioned, secret-free lifecycle event data for external sinks.
/// Destination URLs, identities, credentials, tokens, hashes, and request
/// metadata are intentionally not part of this contract.
/// </summary>
public sealed record ShortLinkLifecycleEvent
{
    public const int CurrentVersion = 1;

    public ShortLinkLifecycleEvent(
        string eventType,
        string code,
        DateTimeOffset occurredAt,
        DateTimeOffset? expiresAt = null,
        bool? isActive = null,
        int version = CurrentVersion)
    {
        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new ArgumentException("An event type is required.", nameof(eventType));
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("A short code is required.", nameof(code));
        }

        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version), version, "Event version must be greater than zero.");
        }

        EventType = eventType;
        Code = code;
        OccurredAt = occurredAt;
        ExpiresAt = expiresAt;
        IsActive = isActive;
        Version = version;
    }

    public int Version { get; }

    public string EventType { get; }

    public string Code { get; }

    public DateTimeOffset OccurredAt { get; }

    public DateTimeOffset? ExpiresAt { get; }

    public bool? IsActive { get; }

    public static ShortLinkLifecycleEvent FromShortLink(
        string eventType,
        ShortLink shortLink,
        DateTimeOffset occurredAt) =>
        new(eventType, shortLink.Code, occurredAt, shortLink.ExpiresAt, shortLink.IsActive);

    public static ShortLinkLifecycleEvent ForCode(
        string eventType,
        string code,
        DateTimeOffset occurredAt) =>
        new(eventType, code, occurredAt);
}
