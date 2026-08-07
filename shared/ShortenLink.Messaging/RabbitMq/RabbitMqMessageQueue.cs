using System.Text.Json;
using System.Threading.Channels;
using RabbitMQ.Client;

namespace ShortenLink.Messaging;

/// <summary>
/// RabbitMQ-backed message queue with explicit receive, acknowledge, and
/// reject boundaries. The consumer channel keeps deliveries unacked until the
/// caller completes the <see cref="MessageDelivery{T}"/>.
/// </summary>
public sealed partial class RabbitMqMessageQueue<T> : IMessageQueue<T>
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
}
