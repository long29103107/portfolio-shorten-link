using ShortenLink.Auditing;
using ShortenLink.Application.Abstractions;
using ShortenLink.Application.Behaviors;
using ShortenLink.Application.Features.Audit;
using ShortenLink.Core.Abstractions;
using ShortenLink.Core.Domain;
using ShortenLink.Mediator;
using Xunit;

namespace ShortenLink.Application.Tests;

public sealed class UnitOfWorkPipelineBehaviorTests
{
    [Fact]
    public async Task Handle_ReturnsBusinessResponseWhenAuditQueueFails()
    {
        var buffer = new AuditEventBuffer();
        var behavior = new UnitOfWorkPipelineBehavior<TestRequest, string>(
            new RecordingUnitOfWork(),
            buffer,
            new ThrowingAuditQueue());

        var result = await behavior.Handle(
            new TestRequest(),
            () =>
            {
                buffer.Add(CreateEvent());
                return Task.FromResult("business-ok");
            },
            CancellationToken.None);

        Assert.Equal("business-ok", result);
        Assert.Empty(buffer.Drain());
    }

    [Fact]
    public async Task Handle_DiscardsBufferedAuditWhenBusinessOperationFails()
    {
        var buffer = new AuditEventBuffer();
        var behavior = new UnitOfWorkPipelineBehavior<TestRequest, string>(
            new RecordingUnitOfWork(),
            buffer,
            new ThrowingAuditQueue());

        await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.Handle(
            new TestRequest(),
            () =>
            {
                buffer.Add(CreateEvent());
                throw new InvalidOperationException("business-failed");
            },
            CancellationToken.None));

        Assert.Empty(buffer.Drain());
    }

    private static AuditEvent CreateEvent() =>
        new(
            "user-1",
            ShortLinkAuditActions.Created,
            "abc1234",
            "user-1",
            DateTimeOffset.UnixEpoch,
            targetType: ShortLinkAuditTargetTypes.ShortLink);

    private sealed record TestRequest : IRequest<string>;

    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public Task<T> ExecuteAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken = default) =>
            operation(cancellationToken);
    }

    private sealed class ThrowingAuditQueue : IAuditEventQueue
    {
        public Task<bool> EnqueueAsync(
            AuditEvent auditEvent,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("audit-storage-failed");
    }
}
