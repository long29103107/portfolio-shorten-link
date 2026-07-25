using ShortenLink.Application.Abstractions;
using ShortenLink.Core.Abstractions;
using ShortenLink.Core.Domain;

namespace ShortenLink.Application.Features.Audit;

public sealed class ShortLinkAuditWriter(
    IShortLinkAuditRepository auditRepository,
    TimeProvider timeProvider)
{
    public Task RecordAsync(
        CurrentRequestActor actor,
        string action,
        string targetId,
        string? ownerUserId,
        string? subjectUserId = null,
        string? detail = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);

        var auditEvent = new ShortLinkAuditEvent(
            GetActorId(actor),
            action,
            targetId,
            ownerUserId,
            timeProvider.GetUtcNow(),
            subjectUserId: subjectUserId,
            detail: detail);

        return auditRepository.AddAsync(auditEvent, cancellationToken);
    }

    private static string GetActorId(CurrentRequestActor actor) =>
        !string.IsNullOrWhiteSpace(actor.ActorId)
            ? actor.ActorId
            : !string.IsNullOrWhiteSpace(actor.UserId)
            ? actor.UserId
            : actor.IsAdmin
                ? "system:admin"
                : "system";
}
