using System.Threading.Channels;

namespace ShortenLink.Messaging;

public sealed class MemoryMessageQueue<T> : IMessageQueue<T>
{
    private readonly Channel<QueueEnvelope<T>> channel;
    private readonly SemaphoreSlim slots;

    public MemoryMessageQueue(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        channel = Channel.CreateBounded<QueueEnvelope<T>>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
        slots = new SemaphoreSlim(capacity, capacity);
    }

    public ValueTask<QueuePublishResult> PublishAsync(
        T message,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!slots.Wait(0))
        {
            return ValueTask.FromResult(QueuePublishResult.Dropped);
        }

        var envelope = new QueueEnvelope<T>(
            message,
            Guid.CreateVersion7().ToString("N"),
            DateTimeOffset.UtcNow);
        if (channel.Writer.TryWrite(envelope))
        {
            return ValueTask.FromResult(QueuePublishResult.Accepted);
        }

        slots.Release();
        return ValueTask.FromResult(QueuePublishResult.Dropped);
    }

    public async IAsyncEnumerable<MessageDelivery<T>> ConsumeAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        await foreach (var envelope in channel.Reader.ReadAllAsync(cancellationToken))
        {
            slots.Release();
            yield return new MessageDelivery<T>(
                envelope,
                _ => ValueTask.CompletedTask,
                async (requeue, ct) =>
                {
                    if (requeue)
                    {
                        await PublishEnvelopeAsync(envelope, ct);
                    }
                });
        }
    }

    public ValueTask DisposeAsync()
    {
        channel.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }

    private ValueTask<QueuePublishResult> PublishEnvelopeAsync(
        QueueEnvelope<T> envelope,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!slots.Wait(0))
        {
            return ValueTask.FromResult(QueuePublishResult.Dropped);
        }

        if (channel.Writer.TryWrite(envelope))
        {
            return ValueTask.FromResult(QueuePublishResult.Accepted);
        }

        slots.Release();
        return ValueTask.FromResult(QueuePublishResult.Dropped);
    }
}
