using ShortenLink.Application.Abstractions;
using ShortenLink.Core.Abstractions;
using ShortenLink.Core.Domain;

namespace ShortenLink.Application.Features.Audit;

public sealed class ShortLinkAuditWriter(
    AuditEventBuffer eventBuffer,
    TimeProvider timeProvider)
{
    public Task RecordAsync(
        CurrentRequestActor actor,
        string action,
        string targetId,
        string? ownerUserId,
        string? subjectUserId = null,
        string? detail = null,
        CancellationToken cancellationToken = default,
        string targetType = ShortLinkAuditTargetTypes.ShortLink)
    {
        ArgumentNullException.ThrowIfNull(actor);

        return RecordAsync(
            GetActorId(actor),
            action,
            targetId,
            ownerUserId,
            subjectUserId: subjectUserId,
            detail: detail,
            targetType: targetType,
            cancellationToken: cancellationToken);
    }

    public Task RecordAsync(
        string actorId,
        string action,
        string targetId,
        string? ownerUserId,
        string? subjectUserId = null,
        string? detail = null,
        string targetType = ShortLinkAuditTargetTypes.ShortLink,
        CancellationToken cancellationToken = default)
    {
        var auditEvent = new ShortLinkAuditEvent(
            actorId,
            action,
            targetId,
            ownerUserId,
            timeProvider.GetUtcNow(),
            subjectUserId: subjectUserId,
            detail: detail,
            targetType: targetType);

        eventBuffer.Add(auditEvent);
        return Task.CompletedTask;
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
