using Microsoft.EntityFrameworkCore;
using ShortenLink.Core.Security;
using ShortenLink.Infrastructure.Persistence;

namespace ShortenLink.Infrastructure.Repositories;

public sealed class EfCoreShortLinkShareRepository(ShortLinkDbContext dbContext)
    : IShortLinkShareRepository
{
    public async Task<IReadOnlyDictionary<string, ShortLinkShareAccess>> ListSharedAccessAsync(
        string userId,
        CancellationToken cancellationToken = default) =>
        (await dbContext.ShortLinkShares
            .AsNoTracking()
            .Where(share => share.UserId == userId)
            .Select(share => new { share.ShortCode, share.Access })
            .ToListAsync(cancellationToken)
            )
        .ToDictionary(share => share.ShortCode, share => share.Access, StringComparer.Ordinal);

    public async Task<IReadOnlyList<ShortLinkShare>> ListByShortCodeAsync(
        string shortCode,
        CancellationToken cancellationToken = default) =>
        await dbContext.ShortLinkShares
            .AsNoTracking()
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
        var record = await dbContext.ShortLinkShares
            .AsNoTracking()
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
        var record = await dbContext.ShortLinkShares
            .FirstOrDefaultAsync(
                item => item.ShortCode == share.ShortCode && item.UserId == share.UserId,
                cancellationToken)
            ;
        if (record is null)
        {
            dbContext.ShortLinkShares.Add(ShortLinkSharePersistenceEntity.FromDomain(share));
        }
        else
        {
            record.UpdateFromDomain(share);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(
        string shortCode,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var deleted = await dbContext.ShortLinkShares
            .Where(share => share.ShortCode == shortCode && share.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken)
            ;
        return deleted > 0;
    }

    public async Task DeleteByShortCodeAsync(
        string shortCode,
        CancellationToken cancellationToken = default)
    {
        await dbContext.ShortLinkShares
            .Where(share => share.ShortCode == shortCode)
            .ExecuteDeleteAsync(cancellationToken)
            ;
    }
}
