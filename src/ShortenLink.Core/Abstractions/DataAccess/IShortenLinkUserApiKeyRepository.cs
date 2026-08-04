using ShortenLink.Core.Security;

namespace ShortenLink.Core.Abstractions;

public interface IShortenLinkUserApiKeyRepository
{
    Task<IReadOnlyList<ShortenLinkUserApiKey>> ListByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<ShortenLinkUserApiKey?> FindByIdAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task<ShortenLinkUserApiKey?> FindByKeyHashAsync(
        string keyHash,
        CancellationToken cancellationToken = default);

    Task AddOrUpdateAsync(
        ShortenLinkUserApiKey apiKey,
        CancellationToken cancellationToken = default);

    Task<bool> DisableAsync(
        string id,
        CancellationToken cancellationToken = default);
}
