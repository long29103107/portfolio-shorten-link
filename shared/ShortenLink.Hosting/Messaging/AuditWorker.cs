using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShortenLink.Core.Abstractions;
using ShortenLink.Core.Domain;
using ShortenLink.Messaging;

namespace ShortenLink.Hosting;

internal sealed class AuditWorker(
    IMessageQueue<AuditEvent> queue,
    IServiceScopeFactory scopeFactory,
    ILogger<AuditWorker> logger)
    : MessageDeliveryWorker<AuditEvent>(queue, scopeFactory, logger)
{
    protected override Task PersistAsync(
        AuditEvent auditEvent,
        IServiceProvider services,
        CancellationToken cancellationToken) =>
        services.GetRequiredService<IAuditRepository>()
            .AddAsync(auditEvent, cancellationToken);

    protected override void LogFailure(
        ILogger logger,
        AuditEvent auditEvent,
        Exception exception) =>
        logger.LogError(
            exception,
            "Failed to persist audit event {Action} for {TargetType}/{TargetId}; business flow remains successful.",
            auditEvent.Action,
            auditEvent.TargetType,
            auditEvent.TargetId);
}
