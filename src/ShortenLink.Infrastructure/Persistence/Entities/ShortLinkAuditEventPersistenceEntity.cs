namespace ShortenLink.Infrastructure.Persistence.Entities;

public sealed class ShortLinkAuditEventPersistenceEntity : BaseEntity<Guid>
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

    public static ShortLinkAuditEventPersistenceEntity FromDomain(ShortLinkAuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        return new ShortLinkAuditEventPersistenceEntity
        {
            Id = auditEvent.Id,
            ActorId = auditEvent.ActorId,
            Action = auditEvent.Action,
            TargetType = auditEvent.TargetType,
            TargetId = auditEvent.TargetId,
            OwnerUserId = auditEvent.OwnerUserId,
            Outcome = auditEvent.Outcome,
            OccurredAt = auditEvent.OccurredAt,
            SubjectUserId = auditEvent.SubjectUserId,
            Detail = auditEvent.Detail,
            CreatedAt = auditEvent.OccurredAt
        };
    }

    public ShortLinkAuditEvent ToDomain() =>
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
