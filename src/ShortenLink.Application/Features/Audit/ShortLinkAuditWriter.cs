using ShortenLink.Application.Abstractions;
using ShortenLink.Auditing;
using ShortenLink.Core.Domain;

namespace ShortenLink.Application.Features.Audit;

public sealed class ShortLinkAuditWriter(
    AuditWriter auditWriter)
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
        auditWriter.Record(
            actorId,
            action,
            targetType,
            targetId,
            ownerId: ownerUserId,
            subjectId: subjectUserId,
            detail: detail);
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
