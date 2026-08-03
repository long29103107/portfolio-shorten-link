using ShortenLink.Core.Domain;

namespace ShortenLink.Application.Abstractions;

/// <summary>
/// Best-effort post-commit audit delivery. Implementations must never throw
/// into the business request when the audit queue is unavailable.
/// </summary>
public interface IAuditEventQueue
{
    Task<bool> EnqueueAsync(
        ShortLinkAuditEvent auditEvent,
        CancellationToken cancellationToken = default);
}
