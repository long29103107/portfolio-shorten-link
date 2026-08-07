using System.Runtime.CompilerServices;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ShortenLink.Messaging;

public sealed partial class RabbitMqMessageQueue<T>
{
    public async IAsyncEnumerable<MessageDelivery<T>> ConsumeAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await EnsureConsumerStartedAsync(cancellationToken);

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            shutdown.Token);

        try
        {
            await foreach (var delivery in deliveries.Reader.ReadAllAsync(linkedCancellation.Token))
                yield return delivery;
        }
        finally
        {
            await StopConsumerAsync();
        }
    }

    private async Task EnsureConsumerStartedAsync(CancellationToken cancellationToken)
    {
        await lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            await EnsureConnectionCreatedAsync(cancellationToken);
            if (consumerChannel is null || !consumerChannel.IsOpen)
            {
                consumerCancellation?.Cancel();
                consumerCancellation = null;
                consumerTag = null;
                if (consumerChannel is not null)
                    await consumerChannel.DisposeAsync();

                consumerChannel = await connection!.CreateChannelAsync(
                    new CreateChannelOptions(false, false, null, 1),
                    cancellationToken);
            }

            if (consumerTag is not null)
                return;

            await DeclareQueueAsync(consumerChannel, cancellationToken);
            await consumerChannel.BasicQosAsync(
                prefetchSize: 0,
                prefetchCount,
                global: false,
                cancellationToken);

            consumerCancellation = CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token);
            var consumer = new AsyncEventingBasicConsumer(consumerChannel);
            consumer.ReceivedAsync += OnReceivedAsync;
            consumerTag = await consumerChannel.BasicConsumeAsync(
                queueName,
                autoAck: false,
                consumer,
                cancellationToken);
        }
        finally
        {
            lifecycleLock.Release();
        }
    }

    private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs args)
    {
        var channel = consumerChannel;
        if (channel is null || !channel.IsOpen)
            return;

        try
        {
            var envelope = JsonSerializer.Deserialize<QueueEnvelope<T>>(
                args.Body.Span,
                SerializerOptions);
            if (envelope is null)
            {
                await channel.BasicNackAsync(
                    args.DeliveryTag,
                    multiple: false,
                    requeue: false,
                    CancellationToken.None);
                return;
            }

            var delivery = new MessageDelivery<T>(
                envelope,
                ct => channel.BasicAckAsync(args.DeliveryTag, multiple: false, ct),
                (requeue, ct) => channel.BasicNackAsync(
                    args.DeliveryTag,
                    multiple: false,
                    requeue,
                    ct));
            var cancellation = consumerCancellation;
            if (cancellation is null)
            {
                await channel.BasicNackAsync(
                    args.DeliveryTag,
                    multiple: false,
                    requeue: true,
                    CancellationToken.None);
                return;
            }

            await deliveries.Writer.WriteAsync(delivery, cancellation.Token);
        }
        catch (JsonException)
        {
            await channel.BasicNackAsync(
                args.DeliveryTag,
                multiple: false,
                requeue: false,
                CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            if (channel.IsOpen)
            {
                await channel.BasicNackAsync(
                    args.DeliveryTag,
                    multiple: false,
                    requeue: true,
                    CancellationToken.None);
            }
        }
    }

    private async Task StopConsumerAsync()
    {
        await lifecycleLock.WaitAsync();
        try
        {
            if (consumerChannel is null || consumerTag is null)
            {
                consumerCancellation?.Cancel();
                consumerCancellation = null;
                return;
            }

            var tag = consumerTag;
            var cancellation = consumerCancellation;
            consumerCancellation = null;
            cancellation?.Cancel();
            try
            {
                if (consumerChannel.IsOpen)
                {
                    await consumerChannel.BasicCancelAsync(
                        tag,
                        noWait: false,
                        CancellationToken.None);
                }
            }
            finally
            {
                consumerTag = null;
            }
        }
        finally
        {
            lifecycleLock.Release();
        }
    }
}
