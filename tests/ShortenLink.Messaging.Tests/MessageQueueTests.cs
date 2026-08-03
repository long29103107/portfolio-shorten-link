using ShortenLink.Messaging;
using Xunit;

namespace ShortenLink.Messaging.Tests;

public sealed class MessageQueueTests
{
    [Fact]
    public async Task MemoryQueue_reports_dropped_messages_when_capacity_is_exhausted()
    {
        await using var queue = new MemoryMessageQueue<string>(capacity: 1);

        var first = await queue.PublishAsync("first");
        var second = await queue.PublishAsync("second");

        Assert.Equal(QueuePublishResult.Accepted, first);
        Assert.Equal(QueuePublishResult.Dropped, second);
    }

    [Fact]
    public async Task MemoryQueue_acknowledges_a_delivery_only_once()
    {
        await using var queue = new MemoryMessageQueue<string>(capacity: 2);
        await queue.PublishAsync("message");

        using var cancellation = new CancellationTokenSource();
        await using var enumerator = queue.ConsumeAsync(cancellation.Token).GetAsyncEnumerator();
        Assert.True(await enumerator.MoveNextAsync());

        var delivery = enumerator.Current;
        Assert.Equal("message", delivery.Message);
        Assert.True(await delivery.AckAsync());
        Assert.False(await delivery.AckAsync());
        Assert.False(await delivery.RejectAsync(requeue: true));
        cancellation.Cancel();
    }

    [Fact]
    public async Task MemoryQueue_can_requeue_a_rejected_delivery()
    {
        await using var queue = new MemoryMessageQueue<string>(capacity: 1);
        await queue.PublishAsync("retry");

        using var cancellation = new CancellationTokenSource();
        await using var enumerator = queue.ConsumeAsync(cancellation.Token).GetAsyncEnumerator();
        Assert.True(await enumerator.MoveNextAsync());

        var delivery = enumerator.Current;
        Assert.True(await delivery.RejectAsync(requeue: true));
        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal("retry", enumerator.Current.Message);
        Assert.True(await enumerator.Current.AckAsync());
        cancellation.Cancel();
    }

    [Fact]
    public async Task MemoryQueue_honors_publish_cancellation()
    {
        await using var queue = new MemoryMessageQueue<string>(capacity: 1);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => queue.PublishAsync("cancelled", cancellation.Token).AsTask());
    }

    [Fact]
    public async Task Factory_creates_memory_provider_without_external_dependencies()
    {
        await using var queue = MessageQueueFactory.Create<string>(
            new MessageQueueOptions { Provider = MessageQueueProvider.Memory, Capacity = 1 },
            "test.queue");

        Assert.IsType<MemoryMessageQueue<string>>(queue);
    }

    [Fact]
    public void Factory_requires_a_connection_string_for_rabbitmq()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            MessageQueueFactory.Create<string>(
                new MessageQueueOptions { Provider = MessageQueueProvider.RabbitMq },
                "test.queue"));

        Assert.Contains("connection string", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
