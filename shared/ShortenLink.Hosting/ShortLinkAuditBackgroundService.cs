using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShortenLink.Core.Abstractions;
using ShortenLink.Core.Domain;
using ShortenLink.Messaging;

namespace ShortenLink.Hosting;

internal sealed class ShortLinkAuditBackgroundService(
    IMessageQueue<ShortLinkAuditEvent> queue,
    IServiceScopeFactory scopeFactory,
    ILogger<ShortLinkAuditBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var delivery in queue.ConsumeAsync(stoppingToken))
        {
            var auditEvent = delivery.Message;
            try
            {
                using var scope = scopeFactory.CreateScope();
                var repository = scope.ServiceProvider
                    .GetRequiredService<IShortLinkAuditRepository>();
                await repository.AddAsync(auditEvent, stoppingToken);
                await delivery.AckAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Failed to persist audit event {Action} for {TargetType}/{TargetId}; business flow remains successful.",
                    auditEvent.Action,
                    auditEvent.TargetType,
                    auditEvent.TargetId);
                // A failed persistence attempt must not hot-loop forever. The
                // durable RabbitMQ adapter can dead-letter this rejection when
                // a DLX is configured; memory mode drops the poison delivery.
                await delivery.RejectAsync(requeue: false, stoppingToken);
            }
        }
    }
}
