using Microsoft.EntityFrameworkCore;
using ShortenLink.Core;
using ShortenLink.Core.Abstractions;
using ShortenLink.Core.Contracts.Expiration;
using ShortenLink.Infrastructure.Persistence;
using ShortenLink.Infrastructure.Persistence.Entities;

namespace ShortenLink.Infrastructure.Repositories;

public sealed class EfCoreShortLinkExpirationCheckpointRepository(ShortLinkDbContext dbContext)
    : EfCoreRepository<ShortLinkExpirationCheckpointPersistenceEntity>(dbContext),
      IShortLinkExpirationCheckpointRepository
{
    public async Task<ShortLinkExpirationCheckpoint?> FindAsync(
        string? tenantId,
        CancellationToken cancellationToken = default)
    {
        var normalizedTenantId = ShortLinkTenantId.Normalize(tenantId) ?? string.Empty;
        var record = await Entities
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == normalizedTenantId, cancellationToken);

        return record is null
            ? null
            : new ShortLinkExpirationCheckpoint(
                string.IsNullOrEmpty(record.TenantId) ? null : record.TenantId,
                record.Cursor,
                record.EvaluatedAtUtc,
                record.CheckpointUpdatedAtUtc);
    }

    public async Task SaveAsync(
        ShortLinkExpirationCheckpoint checkpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);

        var normalizedTenantId = ShortLinkTenantId.Normalize(checkpoint.TenantId) ?? string.Empty;
        var record = await Entities
            .FirstOrDefaultAsync(item => item.TenantId == normalizedTenantId, cancellationToken);
        if (record is null)
        {
            Entities.Add(new ShortLinkExpirationCheckpointPersistenceEntity
            {
                TenantId = normalizedTenantId,
                Cursor = checkpoint.Cursor,
                EvaluatedAtUtc = checkpoint.EvaluatedAtUtc,
                CheckpointUpdatedAtUtc = checkpoint.UpdatedAtUtc
            });
        }
        else
        {
            record.Cursor = checkpoint.Cursor;
            record.EvaluatedAtUtc = checkpoint.EvaluatedAtUtc;
            record.CheckpointUpdatedAtUtc = checkpoint.UpdatedAtUtc;
        }

        await SaveChangesAsync(cancellationToken);
    }
}
