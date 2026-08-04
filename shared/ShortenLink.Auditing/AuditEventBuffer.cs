namespace ShortenLink.Auditing;

public sealed class AuditEventBuffer
{
    private readonly List<AuditEvent> events = [];

    public void Add(AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        events.Add(auditEvent);
    }

    public IReadOnlyList<AuditEvent> Drain()
    {
        if (events.Count == 0)
        {
            return Array.Empty<AuditEvent>();
        }

        var pending = events.ToArray();
        events.Clear();
        return pending;
    }

    public void Clear() => events.Clear();
}
