using ShortenLink.Core.Security;

namespace ShortenLink.Core.Abstractions;

public interface IShortenLinkSecurityRoleRepository
{
    Task<IReadOnlyList<ShortenLinkCustomRole>> ListCustomRolesAsync(
        CancellationToken cancellationToken = default);

    Task<ShortenLinkCustomRole?> FindCustomRoleAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, ShortenLinkCustomRole>> FindCustomRolesAsync(
        IReadOnlyCollection<string> ids,
        CancellationToken cancellationToken = default);

    Task AddOrUpdateCustomRoleAsync(
        ShortenLinkCustomRole role,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShortenLinkRolePermissionOverride>> ListPermissionOverridesAsync(
        string roleId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, IReadOnlyList<ShortenLinkRolePermissionOverride>>> ListPermissionOverridesAsync(
        IReadOnlyCollection<string> roleIds,
        CancellationToken cancellationToken = default);

    Task ReplacePermissionOverridesAsync(
        string roleId,
        IReadOnlyList<ShortenLinkRolePermissionOverride> overrides,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteCustomRoleAsync(
        string id,
        CancellationToken cancellationToken = default);
}
