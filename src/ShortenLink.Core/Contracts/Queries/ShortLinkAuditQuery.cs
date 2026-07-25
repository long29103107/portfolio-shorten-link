namespace ShortenLink.Core.Contracts.Queries;

public sealed record ShortLinkAuditQuery(
    int Limit,
    DateTimeOffset? BeforeOccurredAt,
    Guid? BeforeId,
    string? Action,
    string? TargetId,
    string? ActorId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    ShortLinkAuditAccessScope AccessScope);

public sealed record ShortLinkAuditAccessScope(
    string? UserId,
    bool IsAdmin,
    IReadOnlySet<string> SharedShortCodes);

public sealed record ShortLinkAuditPage(
    IReadOnlyList<Domain.ShortLinkAuditEvent> Items);
