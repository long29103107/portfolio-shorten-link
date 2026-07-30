using ShortenLink.Core.Domain;

namespace ShortenLink.Application.Features.Audit;

public sealed class AuditEventBuffer
{
    private readonly List<ShortLinkAuditEvent> events = [];

    public void Add(ShortLinkAuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        events.Add(auditEvent);
    }

    public IReadOnlyList<ShortLinkAuditEvent> Drain()
    {
        if (events.Count == 0)
        {
            return Array.Empty<ShortLinkAuditEvent>();
        }

        var pending = events.ToArray();
        events.Clear();
        return pending;
    }

    public void Clear() => events.Clear();
}
