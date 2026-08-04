using ShortenLink.Core.Security;

namespace ShortenLink.Core.Abstractions;

public interface IShortenLinkSecurityUserRepository
{
    Task<IReadOnlyList<ShortenLinkSecurityUser>> ListAsync(
        bool includeHidden = false,
        CancellationToken cancellationToken = default);

    Task<ShortenLinkSecurityUser?> FindByIdAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task<ShortenLinkSecurityUser?> FindByUsernameAsync(
        string username,
        CancellationToken cancellationToken = default);

    Task AddOrUpdateAsync(
        ShortenLinkSecurityUser user,
        CancellationToken cancellationToken = default);

    Task<ShortenLinkSecurityUser> EnsureBootstrapAdminAsync(
        string passwordHash,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken = default);

    Task<bool> DisableAsync(
        string id,
        CancellationToken cancellationToken = default);
}
