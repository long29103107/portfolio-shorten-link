namespace ShortenLink.Auditing;

public sealed class AuditEvent
{
    public AuditEvent(
        string actorId,
        string action,
        string targetId,
        string? ownerId,
        DateTimeOffset occurredAt,
        string outcome = AuditOutcomes.Succeeded,
        string? subjectId = null,
        string? detail = null,
        Guid? id = null,
        string targetType = "resource")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetType);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);

        Id = id ?? Guid.CreateVersion7();
        ActorId = actorId.Trim();
        Action = action.Trim();
        TargetType = targetType.Trim();
        TargetId = targetId.Trim();
        OwnerId = Normalize(ownerId);
        OccurredAt = occurredAt;
        Outcome = outcome.Trim();
        SubjectId = Normalize(subjectId);
        Detail = Normalize(detail);
    }

    public Guid Id { get; }

    public string ActorId { get; }

    public string Action { get; }

    public string TargetType { get; }

    public string TargetId { get; }

    public string? OwnerId { get; }

    public string Outcome { get; }

    public DateTimeOffset OccurredAt { get; }

    public string? SubjectId { get; }

    public string? Detail { get; }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public static class AuditOutcomes
{
    public const string Succeeded = "succeeded";
}
