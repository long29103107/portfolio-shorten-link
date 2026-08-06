namespace ShortenLink.Auditing;

public sealed record AuditQuery(
    int Limit,
    DateTimeOffset? BeforeOccurredAt,
    Guid? BeforeId,
    string? FilterExpression,
    AuditReadScope ReadScope);

public sealed record AuditReadScope(
    string? PrincipalId,
    bool HasFullAccess,
    IReadOnlySet<string> AccessibleTargetIds);

public sealed record AuditPage(IReadOnlyList<AuditEvent> Items);
