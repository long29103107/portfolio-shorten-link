namespace ShortenLink.Core.Domain;

public sealed class ShortLinkAuditEvent
{
    public ShortLinkAuditEvent(
        string actorId,
        string action,
        string targetId,
        string? ownerUserId,
        DateTimeOffset occurredAt,
        string outcome = ShortLinkAuditOutcomes.Succeeded,
        string? subjectUserId = null,
        string? detail = null,
        Guid? id = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);

        Id = id ?? Guid.CreateVersion7();
        ActorId = actorId.Trim();
        Action = action.Trim();
        TargetId = targetId.Trim();
        OwnerUserId = Normalize(ownerUserId);
        OccurredAt = occurredAt;
        Outcome = outcome.Trim();
        SubjectUserId = Normalize(subjectUserId);
        Detail = Normalize(detail);
    }

    public Guid Id { get; }

    public string ActorId { get; }

    public string Action { get; }

    public string TargetType => ShortLinkAuditTargetTypes.ShortLink;

    public string TargetId { get; }

    public string? OwnerUserId { get; }

    public string Outcome { get; }

    public DateTimeOffset OccurredAt { get; }

    public string? SubjectUserId { get; }

    public string? Detail { get; }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public static class ShortLinkAuditActions
{
    public const string Created = "short_link.created";
    public const string Updated = "short_link.updated";
    public const string Activated = "short_link.activated";
    public const string Deactivated = "short_link.deactivated";
    public const string Deleted = "short_link.deleted";
    public const string ShareGranted = "short_link.share.granted";
    public const string ShareUpdated = "short_link.share.updated";
    public const string ShareRevoked = "short_link.share.revoked";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(
        [
            Created,
            Updated,
            Activated,
            Deactivated,
            Deleted,
            ShareGranted,
            ShareUpdated,
            ShareRevoked
        ],
        StringComparer.Ordinal);
}

public static class ShortLinkAuditTargetTypes
{
    public const string ShortLink = "short_link";
}

public static class ShortLinkAuditOutcomes
{
    public const string Succeeded = "succeeded";
}
