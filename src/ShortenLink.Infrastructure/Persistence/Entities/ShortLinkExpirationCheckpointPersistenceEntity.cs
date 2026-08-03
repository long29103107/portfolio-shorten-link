using ShortenLink.Core.Domain;

namespace ShortenLink.Infrastructure.Persistence.Entities;

public sealed class ShortLinkExpirationCheckpointPersistenceEntity : BaseEntity<Guid>
{
    public string TenantId { get; set; } = string.Empty;

    public string? Cursor { get; set; }

    public DateTimeOffset EvaluatedAtUtc { get; set; }

    public DateTimeOffset CheckpointUpdatedAtUtc { get; set; }
}
