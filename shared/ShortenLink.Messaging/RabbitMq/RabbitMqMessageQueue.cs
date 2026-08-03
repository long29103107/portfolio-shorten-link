using System.Text.Json;
using System.Threading.Channels;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ShortenLink.Messaging;

/// <summary>
/// RabbitMQ-backed message queue with explicit receive, acknowledge, and
/// reject boundaries. The consumer channel keeps deliveries unacked until the
/// caller completes the <see cref="MessageDelivery{T}"/>.
/// </summary>
public sealed class RabbitMqMessageQueue<T> : IMessageQueue<T>
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private readonly string connectionString;
    private readonly string queueName;
    private readonly ushort prefetchCount;
    private readonly Channel<MessageDelivery<T>> deliveries;
    private readonly SemaphoreSlim lifecycleLock = new(1, 1);
    private readonly CancellationTokenSource shutdown = new();

    private IConnection? connection;
    private IChannel? publisherChannel;
    private IChannel? consumerChannel;
    private string? consumerTag;
    private CancellationTokenSource? consumerCancellation;
    private bool disposed;

    public RabbitMqMessageQueue(
        string? connectionString,
        string queueName,
        int capacity,
        ushort prefetchCount)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException(
                "A RabbitMQ connection string is required when the RabbitMq provider is selected.",
                nameof(connectionString));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        this.connectionString = connectionString;
        this.queueName = queueName;
        this.prefetchCount = (ushort)Math.Min(
            prefetchCount == 0 ? 1 : prefetchCount,
            Math.Min(capacity, ushort.MaxValue));
        deliveries = Channel.CreateBounded<MessageDelivery<T>>(
            new BoundedChannelOptions(capacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait
            });
    }

    public async ValueTask<QueuePublishResult> PublishAsync(
        T message,
        CancellationToken cancellationToken = default)
    {
        await EnsurePublisherStartedAsync(cancellationToken);

        var envelope = new QueueEnvelope<T>(
            message,
            Guid.CreateVersion7().ToString("N"),
            DateTimeOffset.UtcNow);
        var body = JsonSerializer.SerializeToUtf8Bytes(envelope, SerializerOptions);
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            MessageId = envelope.MessageId,
            DeliveryMode = DeliveryModes.Persistent
        };

        await publisherChannel!.BasicPublishAsync(
            string.Empty,
            queueName,
            mandatory: true,
            basicProperties: properties,
            body,
            cancellationToken);

        // Publisher confirmations are enabled on the channel, so completion
        // means the broker accepted the message rather than only buffering it
        // in the client library.
        return QueuePublishResult.Accepted;
    }

    public async IAsyncEnumerable<MessageDelivery<T>> ConsumeAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        await EnsureConsumerStartedAsync(cancellationToken);

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            shutdown.Token);

        try
        {
            await foreach (var delivery in deliveries.Reader.ReadAllAsync(linkedCancellation.Token))
            {
                yield return delivery;
            }
        }
        finally
        {
            await StopConsumerAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await lifecycleLock.WaitAsync();
        try
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            shutdown.Cancel();
            deliveries.Writer.TryComplete();

            if (consumerChannel is not null && consumerTag is not null)
            {
                try
                {
                    await consumerChannel.BasicCancelAsync(
                        consumerTag,
                        noWait: false,
                        CancellationToken.None);
                }
                catch (Exception) when (!consumerChannel.IsOpen)
                {
                    // Automatic recovery or broker shutdown already closed it.
                }
            }

            if (publisherChannel is not null)
            {
                await publisherChannel.DisposeAsync();
            }

            if (consumerChannel is not null)
            {
                await consumerChannel.DisposeAsync();
            }

            if (connection is not null)
            {
                await connection.DisposeAsync();
            }
        }
        finally
        {
            lifecycleLock.Release();
        }
    }

    private async Task EnsurePublisherStartedAsync(CancellationToken cancellationToken)
    {
        await lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            await EnsureConnectionCreatedAsync(cancellationToken);
            if (publisherChannel is not null && publisherChannel.IsOpen)
            {
                return;
            }

            if (publisherChannel is not null)
            {
                await publisherChannel.DisposeAsync();
            }

            publisherChannel = await connection!.CreateChannelAsync(
                new CreateChannelOptions(true, true),
                cancellationToken);
            await DeclareQueueAsync(publisherChannel, cancellationToken);
        }
        finally
        {
            lifecycleLock.Release();
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
                {
                    await consumerChannel.DisposeAsync();
                }

                consumerChannel = await connection!.CreateChannelAsync(
                    new CreateChannelOptions(false, false, null, 1),
                    cancellationToken);
            }

            if (consumerTag is not null)
            {
                return;
            }

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

    private async Task EnsureConnectionCreatedAsync(CancellationToken cancellationToken)
    {
        if (connection is not null)
        {
            return;
        }

        var factory = new ConnectionFactory
        {
            Uri = new Uri(connectionString),
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true
        };
        connection = await factory.CreateConnectionAsync(cancellationToken);
    }

    private async Task DeclareQueueAsync(
        IChannel targetChannel,
        CancellationToken cancellationToken)
    {
        await targetChannel.QueueDeclareAsync(
            queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);
    }

    private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs args)
    {
        var channel = consumerChannel;
        if (channel is null || !channel.IsOpen)
        {
            return;
        }

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
                ct => channel.BasicAckAsync(
                    args.DeliveryTag,
                    multiple: false,
                    ct),
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

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}
