using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using ShortenLink.Application.Abstractions;
using ShortenLink.Core.Domain;

namespace ShortenLink.Hosting;

internal sealed class ChannelShortLinkAuditEventQueue(
    Channel<ShortLinkAuditEvent> channel,
    ILogger<ChannelShortLinkAuditEventQueue> logger) : IAuditEventQueue
{
    public bool TryEnqueue(ShortLinkAuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        if (channel.Writer.TryWrite(auditEvent))
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
