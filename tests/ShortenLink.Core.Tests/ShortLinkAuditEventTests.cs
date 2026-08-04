using ShortenLink.Auditing;
using ShortenLink.Core.Domain;
using Xunit;

namespace ShortenLink.Core.Tests;

public sealed class AuditEventTests
{
    [Fact]
    public void Constructor_PreservesExplicitIdentityTargetContract()
    {
        var auditEvent = new AuditEvent(
            "user-1",
            ShortLinkAuditActions.UserApiKeyCreated,
            "api-key-1",
            "user-1",
            DateTimeOffset.UnixEpoch,
            subjectId: "user-1",
            targetType: ShortLinkAuditTargetTypes.UserApiKey);

        Assert.Equal(ShortLinkAuditTargetTypes.UserApiKey, auditEvent.TargetType);
        Assert.Contains(auditEvent.Action, ShortLinkAuditActions.All);
        Assert.Contains(auditEvent.TargetType, ShortLinkAuditTargetTypes.All);
        Assert.Null(auditEvent.Detail);
    }
}
