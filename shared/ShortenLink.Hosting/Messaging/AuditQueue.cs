using Microsoft.Extensions.Logging;
using ShortenLink.Application.Abstractions;
using ShortenLink.Core.Domain;
using ShortenLink.Messaging;

namespace ShortenLink.Hosting;

internal sealed class AuditQueue(
    IMessageQueue<AuditEvent> queue,
    ILogger<AuditQueue> logger) : IAuditEventQueue
{
    public async Task<bool> EnqueueAsync(
        AuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        var result = await queue.PublishAsync(auditEvent, cancellationToken);
        if (result == QueuePublishResult.Accepted)
        {
            return true;
        }

        logger.LogWarning(
            "Audit event queue is full; dropping audit event {Action} for {TargetType}/{TargetId}.",
            auditEvent.Action,
            auditEvent.TargetType,
            auditEvent.TargetId);
        return false;
    }
}
