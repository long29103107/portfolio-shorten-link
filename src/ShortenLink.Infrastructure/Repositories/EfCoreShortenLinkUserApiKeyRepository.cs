using Microsoft.EntityFrameworkCore;
using ShortenLink.Core.Security;
using ShortenLink.Infrastructure.Persistence;

namespace ShortenLink.Infrastructure.Repositories;

public sealed class EfCoreShortenLinkUserApiKeyRepository(ShortLinkDbContext dbContext)
    : EfCoreRepository<ShortenLinkUserApiKeyPersistenceEntity>(dbContext),
        IShortenLinkUserApiKeyRepository
{
    public async Task<IReadOnlyList<ShortenLinkUserApiKey>> ListByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var records = await ReadOnlyEntities
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

        var record = await ReadOnlyEntities
            .FirstOrDefaultAsync(apiKey => apiKey.ApiKeyId == id, cancellationToken)
            ;

        return record?.ToDomain();
    }

    public async Task<ShortenLinkUserApiKey?> FindByKeyHashAsync(
        string keyHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyHash);

        var record = await ReadOnlyEntities
            .FirstOrDefaultAsync(apiKey => apiKey.KeyHash == keyHash, cancellationToken)
            ;

        return record?.ToDomain();
    }

    public async Task AddOrUpdateAsync(
        ShortenLinkUserApiKey apiKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(apiKey);

        var record = await Entities
            .FirstOrDefaultAsync(candidate => candidate.ApiKeyId == apiKey.ApiKeyKey, cancellationToken)
            ;

        if (record is null)
        {
            Entities.Add(ShortenLinkUserApiKeyPersistenceEntity.FromDomain(apiKey));
        }
        else
        {
            record.UpdateFromDomain(apiKey);
        }

        await SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DisableAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var record = await Entities
            .FirstOrDefaultAsync(apiKey => apiKey.ApiKeyId == id, cancellationToken)
            ;
        if (record is null)
        {
            return false;
        }

        record.IsEnabled = false;
        await SaveChangesAsync(cancellationToken);
        return true;
    }
}
