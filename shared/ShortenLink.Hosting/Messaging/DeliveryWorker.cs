using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShortenLink.Messaging;

namespace ShortenLink.Hosting;

/// <summary>
/// Owns queue delivery completion semantics. Concrete workers only map a
/// message to its repository operation and its failure diagnostic.
/// </summary>
internal abstract class MessageDeliveryWorker<TMessage>(
    IMessageQueue<TMessage> queue,
    IServiceScopeFactory scopeFactory,
    ILogger logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var delivery in queue.ConsumeAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await PersistAsync(delivery.Message, scope.ServiceProvider, stoppingToken);
                await delivery.AckAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogFailure(logger, delivery.Message, exception);
                // Keep poison messages from being requeued indefinitely. The
                // durable RabbitMQ adapter can dead-letter this rejection when
                // a DLX is configured; memory mode drops the poison delivery.
                await delivery.RejectAsync(requeue: false, stoppingToken);
            }
        }
    }

    protected abstract Task PersistAsync(
        TMessage message,
        IServiceProvider services,
        CancellationToken cancellationToken);

    protected abstract void LogFailure(
        ILogger logger,
        TMessage message,
        Exception exception);
}
