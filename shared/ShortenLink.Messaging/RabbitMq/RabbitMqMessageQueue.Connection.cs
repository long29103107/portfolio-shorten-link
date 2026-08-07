using RabbitMQ.Client;

namespace ShortenLink.Messaging;

public sealed partial class RabbitMqMessageQueue<T>
{
    public async ValueTask DisposeAsync()
    {
        await lifecycleLock.WaitAsync();
        try
        {
            if (disposed)
                return;

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
                await publisherChannel.DisposeAsync();

            if (consumerChannel is not null)
                await consumerChannel.DisposeAsync();

            if (connection is not null)
                await connection.DisposeAsync();
        }
        finally
        {
            lifecycleLock.Release();
        }
    }

    private async Task EnsureConnectionCreatedAsync(CancellationToken cancellationToken)
    {
        if (connection is not null)
            return;

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

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}
