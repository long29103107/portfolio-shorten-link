using System.Text.Json;
using RabbitMQ.Client;

namespace ShortenLink.Messaging;

public sealed partial class RabbitMqMessageQueue<T>
{
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

        return QueuePublishResult.Accepted;
    }

    private async Task EnsurePublisherStartedAsync(CancellationToken cancellationToken)
    {
        await lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            await EnsureConnectionCreatedAsync(cancellationToken);
            if (publisherChannel is not null && publisherChannel.IsOpen)
                return;

            if (publisherChannel is not null)
                await publisherChannel.DisposeAsync();

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
}
