namespace ShortenLink.Auditing;

public sealed class AuditWriter(
    AuditEventBuffer eventBuffer,
    TimeProvider timeProvider)
{
    public AuditEvent Record(
        string actorId,
        string action,
        string targetType,
        string targetId,
        string? ownerId = null,
        string? subjectId = null,
        string? detail = null,
        string outcome = AuditOutcomes.Succeeded)
    {
        var auditEvent = new AuditEvent(
            actorId,
            action,
            targetId,
            ownerId,
            timeProvider.GetUtcNow(),
            outcome,
            subjectId,
            detail,
            targetType: targetType);

        eventBuffer.Add(auditEvent);
        return auditEvent;
    }
}
