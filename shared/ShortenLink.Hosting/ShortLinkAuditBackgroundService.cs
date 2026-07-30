using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShortenLink.Core.Domain;
using ShortenLink.Core.Abstractions;

namespace ShortenLink.Hosting;

internal sealed class ShortLinkAuditBackgroundService(
    Channel<ShortLinkAuditEvent> channel,
    IServiceScopeFactory scopeFactory,
    ILogger<ShortLinkAuditBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var auditEvent in channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var repository = scope.ServiceProvider
                    .GetRequiredService<IShortLinkAuditRepository>();
                await repository.AddAsync(auditEvent, stoppingToken);
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
            }
        }
    }
}
