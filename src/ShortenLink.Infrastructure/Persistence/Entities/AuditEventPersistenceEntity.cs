namespace ShortenLink.Infrastructure.Persistence.Entities;

public sealed class AuditEventPersistenceEntity : BaseEntity<Guid>
{
    public string ActorId { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string TargetType { get; set; } = string.Empty;

    public string TargetId { get; set; } = string.Empty;

    public string? OwnerUserId { get; set; }

    public string Outcome { get; set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; set; }

    public string? SubjectUserId { get; set; }

    public string? Detail { get; set; }

    public static AuditEventPersistenceEntity FromDomain(AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        return new AuditEventPersistenceEntity
        {
            Id = auditEvent.Id,
            ActorId = auditEvent.ActorId,
            Action = auditEvent.Action,
            TargetType = auditEvent.TargetType,
            TargetId = auditEvent.TargetId,
            OwnerUserId = auditEvent.OwnerId,
            Outcome = auditEvent.Outcome,
            OccurredAt = auditEvent.OccurredAt,
            SubjectUserId = auditEvent.SubjectId,
            Detail = auditEvent.Detail,
            CreatedAt = auditEvent.OccurredAt
        };
    }

    public AuditEvent ToDomain() =>
        new(
            ActorId,
            Action,
            TargetId,
            OwnerUserId,
            OccurredAt,
            Outcome,
            SubjectUserId,
            Detail,
            Id,
            TargetType);
}
