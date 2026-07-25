using ShortenLink.Application.Abstractions;
using ShortenLink.Application.Features.Audit;
using ShortenLink.Core.Abstractions;
using ShortenLink.Core.Contracts.Queries;
using ShortenLink.Core.Domain;
using Xunit;

namespace ShortenLink.Application.Tests;

public sealed class ShortLinkAuditWriterTests
{
    [Fact]
    public async Task RecordAsync_AppendsStableActorTargetAndNonSecretContext()
    {
        var repository = new CapturingAuditRepository();
        var occurredAt = new DateTimeOffset(2026, 7, 25, 9, 0, 0, TimeSpan.Zero);
        var writer = new ShortLinkAuditWriter(
            repository,
            new FixedTimeProvider(occurredAt));

        await writer.RecordAsync(
            new CurrentRequestActor("user-1", IsAdmin: false),
            ShortLinkAuditActions.ShareGranted,
            "abc1234",
            "owner-1",
            "user-2",
            "View");

        var auditEvent = Assert.Single(repository.Events);
        Assert.Equal("user-1", auditEvent.ActorId);
        Assert.Equal("abc1234", auditEvent.TargetId);
        Assert.Equal("owner-1", auditEvent.OwnerUserId);
        Assert.Equal("user-2", auditEvent.SubjectUserId);
        Assert.Equal("View", auditEvent.Detail);
        Assert.Equal(occurredAt, auditEvent.OccurredAt);
        Assert.Equal(ShortLinkAuditOutcomes.Succeeded, auditEvent.Outcome);
    }

    [Fact]
    public async Task RecordAsync_UsesStableSystemActorWhenCredentialHasNoUser()
    {
        var repository = new CapturingAuditRepository();
        var writer = new ShortLinkAuditWriter(
            repository,
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));

        await writer.RecordAsync(
            new CurrentRequestActor(null, IsAdmin: true),
            ShortLinkAuditActions.Created,
            "abc1234",
            null);

        Assert.Equal("system:admin", Assert.Single(repository.Events).ActorId);
    }

    private sealed class CapturingAuditRepository : IShortLinkAuditRepository
    {
        public List<ShortLinkAuditEvent> Events { get; } = [];

        public Task AddAsync(
            ShortLinkAuditEvent auditEvent,
            CancellationToken cancellationToken = default)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }

        public Task<ShortLinkAuditPage> ListAsync(
            ShortLinkAuditQuery query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ShortLinkAuditPage(Events));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
