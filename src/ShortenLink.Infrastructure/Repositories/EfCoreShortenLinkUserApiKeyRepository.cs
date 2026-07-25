using Microsoft.EntityFrameworkCore;
using ShortenLink.Core.Security;
using ShortenLink.Infrastructure.Persistence;

namespace ShortenLink.Infrastructure.Repositories;

public sealed class EfCoreShortenLinkUserApiKeyRepository : IShortenLinkUserApiKeyRepository
{
    private readonly ShortLinkDbContext dbContext;

    public EfCoreShortenLinkUserApiKeyRepository(ShortLinkDbContext dbContext)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<IReadOnlyList<ShortenLinkUserApiKey>> ListByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var records = await dbContext.SecurityUserApiKeys
            .AsNoTracking()
            .Where(apiKey => apiKey.UserId == userId)
            .ToListAsync(cancellationToken)
            ;

        return records
            .OrderBy(apiKey => apiKey.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(apiKey => apiKey.CreatedAt)
            .Select(apiKey => apiKey.ToDomain())
            .ToList();
    }

    public async Task<ShortenLinkUserApiKey?> FindByIdAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var record = await dbContext.SecurityUserApiKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(apiKey => apiKey.ApiKeyId == id, cancellationToken)
            ;

        return record?.ToDomain();
    }

    public async Task<ShortenLinkUserApiKey?> FindByKeyHashAsync(
        string keyHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyHash);

        var record = await dbContext.SecurityUserApiKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(apiKey => apiKey.KeyHash == keyHash, cancellationToken)
            ;

        return record?.ToDomain();
    }

    public async Task AddOrUpdateAsync(
        ShortenLinkUserApiKey apiKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(apiKey);

        var record = await dbContext.SecurityUserApiKeys
            .FirstOrDefaultAsync(candidate => candidate.ApiKeyId == apiKey.ApiKeyKey, cancellationToken)
            ;

        if (record is null)
        {
            dbContext.SecurityUserApiKeys.Add(ShortenLinkUserApiKeyPersistenceEntity.FromDomain(apiKey));
        }
        else
        {
            record.UpdateFromDomain(apiKey);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DisableAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var record = await dbContext.SecurityUserApiKeys
            .FirstOrDefaultAsync(apiKey => apiKey.ApiKeyId == id, cancellationToken)
            ;
        if (record is null)
        {
            return false;
        }

        record.IsEnabled = false;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
