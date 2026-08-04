namespace ShortenLink.Auditing;

public interface IAuditRepository
{
    Task AddAsync(
        AuditEvent auditEvent,
        CancellationToken cancellationToken = default);

    Task<AuditPage> ListAsync(
        AuditQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListActionsAsync(
        AuditReadScope readScope,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Best-effort post-commit audit delivery. Implementations should not make a
/// completed business transaction fail when audit delivery is unavailable.
/// </summary>
public interface IAuditEventQueue
{
    Task<bool> EnqueueAsync(
        AuditEvent auditEvent,
        CancellationToken cancellationToken = default);
}
