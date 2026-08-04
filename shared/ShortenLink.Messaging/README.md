# ShortenLink.Messaging

`ShortenLink.Messaging` is the provider-neutral queue boundary used by
ShortenLink background work. The same `IMessageQueue<T>` contract supports a
bounded in-memory queue for local development and RabbitMQ for distributed
deployments.

## Install and reference

```xml
<ProjectReference Include="..\shared\ShortenLink.Messaging\ShortenLink.Messaging.csproj" />
```

The package is also packable as `ShortenLink.Messaging`.

## Memory provider

Memory is the default and needs no broker:

```csharp
await using var queue = MessageQueueFactory.Create<AuditEvent>(
    new MessageQueueOptions
    {
        Provider = MessageQueueProvider.Memory,
        Capacity = 512
    },
    "shorten-link.audit");

var publishResult = await queue.PublishAsync(auditEvent, cancellationToken);
if (publishResult == QueuePublishResult.Dropped)
{
    // The bounded memory queue is full; decide whether to log or retry.
}

await foreach (var delivery in queue.ConsumeAsync(cancellationToken))
{
    await HandleAsync(delivery.Message, cancellationToken);
    await delivery.AckAsync(cancellationToken);
}
```

Each delivery has one terminal acknowledgement: `AckAsync` or
`RejectAsync` returns `true` only for the first call. A rejected delivery can
be requeued with `RejectAsync(requeue: true, cancellationToken)`; use `false`
when the message should be discarded.

When a bounded memory queue is full, `PublishAsync` returns `Dropped`. This
keeps the existing local fire-and-forget behavior explicit instead of blocking
request processing.

## RabbitMQ provider

RabbitMQ is opt-in and requires an AMQP URI:

```csharp
var queue = MessageQueueFactory.Create<AuditEvent>(
    new MessageQueueOptions
    {
        Provider = MessageQueueProvider.RabbitMq,
        RabbitMqConnectionString = "amqp://user:password@localhost:5672/",
        Capacity = 512,
        PrefetchCount = 16
    },
    "shorten-link.audit");
```

Messages are JSON envelopes with a generated message id. RabbitMQ queues are
durable, publishers use a dedicated publisher-confirm channel with persistent
delivery mode, and consumers use a separate manual-acknowledgement channel.
Prefetch is bounded by the local delivery buffer, so a message remains
unacked until the handler calls `AckAsync`; `RejectAsync(requeue: true)` returns
a failed delivery to the broker. Credentials must come from configuration or a
secret store; never commit them to source control or log the connection string.

## Provider selection in ShortenLink.Hosting

The host binds queue settings from the `ShortenLink:Queue` section. Memory is
the safe local default:

```json
{
  "ShortenLink": {
    "Queue": {
      "Provider": "Memory",
      "AuditQueueName": "shorten-link.audit",
      "AnalyticsQueueName": "shorten-link.analytics",
      "AuditCapacity": 1024,
      "AnalyticsCapacity": 512,
      "PrefetchCount": 16
    }
  }
}
```

For RabbitMQ, set `Provider` to `RabbitMq` and supply
`RabbitMqConnectionString` through an environment variable or secret-backed
configuration. Audit and click consumers use the same abstraction; application
handlers do not need provider-specific code.
