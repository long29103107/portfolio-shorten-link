using ShortenLink.Core.Domain;

namespace ShortenLink.Infrastructure.Persistence.Entities;

public sealed class ShortLinkBulkJobPersistenceEntity : BaseEntity<Guid>
{
    public ShortLinkBulkJobPersistenceEntity()
    {
    }

    public ShortLinkBulkJobPersistenceEntity(Guid id)
        : base(DateTimeOffset.UtcNow, id)
    {
    }

    public string Operation { get; set; } = string.Empty;
    public string CodesJson { get; set; } = "[]";
    public string? Folder { get; set; }
    public string TagsJson { get; set; } = "[]";
    public string Status { get; set; } = "queued";
    public int TotalCount { get; set; }
    public int ProcessedCount { get; set; }
    public int SucceededCount { get; set; }
    public int FailedCount { get; set; }
    public string? ResultJson { get; set; }
    public string? Error { get; set; }
    public string? ActorId { get; set; }
    public string? UserId { get; set; }
    public bool IsAdmin { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string? IdempotencyKey { get; set; }
    public string RequestHash { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public DateTimeOffset? LastHeartbeatAtUtc { get; set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }
    public bool CancellationRequested { get; set; }
}
