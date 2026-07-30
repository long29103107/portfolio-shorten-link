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
        var eventBuffer = new AuditEventBuffer();
        var occurredAt = new DateTimeOffset(2026, 7, 25, 9, 0, 0, TimeSpan.Zero);
        var writer = new ShortLinkAuditWriter(
            eventBuffer,
            new FixedTimeProvider(occurredAt));

        await writer.RecordAsync(
            new CurrentRequestActor("user-1", IsAdmin: false),
            ShortLinkAuditActions.ShareGranted,
            "abc1234",
            "owner-1",
            "user-2",
            "View");

        var auditEvent = Assert.Single(eventBuffer.Drain());
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
        var eventBuffer = new AuditEventBuffer();
        var writer = new ShortLinkAuditWriter(
            eventBuffer,
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));

        await writer.RecordAsync(
            new CurrentRequestActor(null, IsAdmin: true),
            ShortLinkAuditActions.Created,
            "abc1234",
            null);

        Assert.Equal("system:admin", Assert.Single(eventBuffer.Drain()).ActorId);
    }

    [Fact]
    public async Task RecordAsync_WritesIdentityTargetWithoutSecretContext()
    {
        var eventBuffer = new AuditEventBuffer();
        var writer = new ShortLinkAuditWriter(
            eventBuffer,
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));

        await writer.RecordAsync(
            "user-1",
            ShortLinkAuditActions.AuthenticationRefresh,
            "user-1",
            "user-1",
            subjectUserId: "user-1",
            targetType: ShortLinkAuditTargetTypes.Authentication);

        var auditEvent = Assert.Single(eventBuffer.Drain());
        Assert.Equal(ShortLinkAuditTargetTypes.Authentication, auditEvent.TargetType);
        Assert.Equal("user-1", auditEvent.ActorId);
        Assert.Equal("user-1", auditEvent.OwnerUserId);
        Assert.Null(auditEvent.Detail);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
