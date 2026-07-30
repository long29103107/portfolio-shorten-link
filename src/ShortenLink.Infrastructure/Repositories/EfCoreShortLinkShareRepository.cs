using Microsoft.EntityFrameworkCore;
using ShortenLink.Core.Security;
using ShortenLink.Infrastructure.Persistence;

namespace ShortenLink.Infrastructure.Repositories;

public sealed class EfCoreShortLinkShareRepository(ShortLinkDbContext dbContext)
    : EfCoreRepository<ShortLinkSharePersistenceEntity>(dbContext), IShortLinkShareRepository
{
    public async Task<IReadOnlyDictionary<string, ShortLinkShareAccess>> ListSharedAccessAsync(
        string userId,
        CancellationToken cancellationToken = default) =>
        (await ReadOnlyEntities
            .Where(share => share.UserId == userId)
            .Select(share => new { share.ShortCode, share.Access })
            .ToListAsync(cancellationToken)
            )
        .ToDictionary(share => share.ShortCode, share => share.Access, StringComparer.Ordinal);

    public async Task<IReadOnlyList<ShortLinkShare>> ListByShortCodeAsync(
        string shortCode,
        CancellationToken cancellationToken = default) =>
        await ReadOnlyEntities
            .Where(share => share.ShortCode == shortCode)
            .OrderBy(share => share.UserId)
            .Select(share => share.ToDomain())
            .ToListAsync(cancellationToken)
            ;

    public async Task<ShortLinkShare?> FindAsync(
        string shortCode,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var record = await ReadOnlyEntities
            .FirstOrDefaultAsync(
                share => share.ShortCode == shortCode && share.UserId == userId,
                cancellationToken)
            ;
        return record?.ToDomain();
    }

    public async Task AddOrUpdateAsync(
        ShortLinkShare share,
        CancellationToken cancellationToken = default)
    {
        var record = await Entities
            .FirstOrDefaultAsync(
                item => item.ShortCode == share.ShortCode && item.UserId == share.UserId,
                cancellationToken)
            ;
        if (record is null)
        {
            Entities.Add(ShortLinkSharePersistenceEntity.FromDomain(share));
        }
        else
        {
            record.UpdateFromDomain(share);
        }
        await SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(
        string shortCode,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var deleted = await Entities
            .Where(share => share.ShortCode == shortCode && share.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken)
            ;
        return deleted > 0;
    }

    public async Task DeleteByShortCodeAsync(
        string shortCode,
        CancellationToken cancellationToken = default)
    {
        await Entities
            .Where(share => share.ShortCode == shortCode)
            .ExecuteDeleteAsync(cancellationToken)
            ;
    }
}
