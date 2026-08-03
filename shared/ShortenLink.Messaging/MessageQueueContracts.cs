namespace ShortenLink.Messaging;

public enum MessageQueueProvider
{
    Memory,
    RabbitMq
}

public enum QueuePublishResult
{
    Accepted,
    Dropped
}

public sealed class MessageQueueOptions
{
    public MessageQueueProvider Provider { get; set; } = MessageQueueProvider.Memory;

    public int Capacity { get; set; } = 512;

    public ushort PrefetchCount { get; set; } = 16;

    public string? RabbitMqConnectionString { get; set; }
}

public sealed record QueueEnvelope<T>(
    T Message,
    string MessageId,
    DateTimeOffset EnqueuedAtUtc);

public interface IMessageQueue<T> : IAsyncDisposable
{
    ValueTask<QueuePublishResult> PublishAsync(
        T message,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<MessageDelivery<T>> ConsumeAsync(
        CancellationToken cancellationToken = default);
}

public sealed class MessageDelivery<T>
{
    private readonly Func<CancellationToken, ValueTask> acknowledge;
    private readonly Func<bool, CancellationToken, ValueTask> reject;
    private int completed;

    internal MessageDelivery(
        QueueEnvelope<T> envelope,
        Func<CancellationToken, ValueTask> acknowledge,
        Func<bool, CancellationToken, ValueTask> reject)
    {
        Envelope = envelope;
        this.acknowledge = acknowledge;
        this.reject = reject;
    }

    public QueueEnvelope<T> Envelope { get; }

    public T Message => Envelope.Message;

    public ValueTask<bool> AckAsync(CancellationToken cancellationToken = default) =>
        CompleteAsync(() => acknowledge(cancellationToken));

    public ValueTask<bool> RejectAsync(
        bool requeue,
        CancellationToken cancellationToken = default) =>
        CompleteAsync(() => reject(requeue, cancellationToken));

    private async ValueTask<bool> CompleteAsync(Func<ValueTask> completion)
    {
        if (Interlocked.Exchange(ref completed, 1) != 0)
        {
            return false;
        }

        await completion();
        return true;
    }
}

public static class MessageQueueFactory
{
    public static IMessageQueue<T> Create<T>(
        MessageQueueOptions options,
        string queueName)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        return options.Provider switch
        {
            MessageQueueProvider.Memory =>
                new MemoryMessageQueue<T>(options.Capacity),
            MessageQueueProvider.RabbitMq =>
                new RabbitMqMessageQueue<T>(
                    options.RabbitMqConnectionString,
                    queueName,
                    options.Capacity,
                    options.PrefetchCount),
            _ => throw new ArgumentOutOfRangeException(nameof(options.Provider))
        };
    }
}
