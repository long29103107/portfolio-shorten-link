using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShortenLink.Core.Domain;
using ShortenLink.Core.Services;
using ShortenLink.Messaging;

namespace ShortenLink.Hosting;

internal sealed class ClickWorker(
    IMessageQueue<RecordShortLinkClickRequest> queue,
    IServiceScopeFactory scopeFactory,
    ILogger<ClickWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var delivery in queue.ConsumeAsync(stoppingToken))
        {
            var request = delivery.Message;
            try
            {
                using var scope = scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IShortLinkClickRepository>();
                var shortLinkClick = new ShortLinkClickEntity(
                    request.ShortCode,
                    request.ClickedAtUtc,
                    request.RemoteIpAddress,
                    request.UserAgent,
                    request.Referrer,
                    tenantId: request.TenantId);

                await repository.AddAsync(shortLinkClick, stoppingToken);
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
                    "Failed to persist short-link click analytics event for code {ShortCode}.",
                    request.ShortCode);
                // Keep poison messages from being requeued indefinitely. The
                // durable RabbitMQ adapter can dead-letter this rejection when
                // a DLX is configured; memory mode drops the poison delivery.
                await delivery.RejectAsync(requeue: false, stoppingToken);
            }
        }
    }
}
