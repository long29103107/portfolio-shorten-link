using Microsoft.EntityFrameworkCore;
using ShortenLink.Core.Domain;
using ShortenLink.Infrastructure.Persistence;

namespace ShortenLink.Infrastructure.Repositories;

public abstract class EfCoreRepository<TEntity>
    where TEntity : BaseEntity<Guid>
{
    protected EfCoreRepository(ShortLinkDbContext dbContext)
    {
        DbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    protected ShortLinkDbContext DbContext { get; }

    protected DbSet<TEntity> Entities => DbContext.Set<TEntity>();

    protected IQueryable<TEntity> ReadOnlyEntities => Entities.AsNoTracking();

    protected async Task AddEntityAsync(
        TEntity entity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        Entities.Add(entity);
        await SaveChangesAsync(cancellationToken);
    }

    protected Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        DbContext.SaveChangesAsync(cancellationToken);
}
