using ShortenLink.Core.Security;

namespace ShortenLink.Core.Abstractions;

public interface IShortLinkShareRepository
{
    Task<IReadOnlyDictionary<string, ShortLinkShareAccess>> ListSharedAccessAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShortLinkShare>> ListByShortCodeAsync(
        string shortCode,
        CancellationToken cancellationToken = default);

    Task<ShortLinkShare?> FindAsync(
        string shortCode,
        string userId,
        CancellationToken cancellationToken = default);

    Task AddOrUpdateAsync(
        ShortLinkShare share,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        string shortCode,
        string userId,
        CancellationToken cancellationToken = default);

    Task DeleteByShortCodeAsync(
        string shortCode,
        CancellationToken cancellationToken = default);
}
