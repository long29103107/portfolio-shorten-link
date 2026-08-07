using ShortenLink.Core.Security;

namespace ShortenLink.Hosting;

public sealed partial class UserSessionService
{
    public async Task<ShortenLinkUserSessionPrincipal> CreatePrincipalAsync(
        ShortenLinkSecurityUser user,
        DateTimeOffset issuedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var permissions = new HashSet<string>(StringComparer.Ordinal);
        var roleIds = user.RoleIds
            .Where(static roleId => !string.IsNullOrWhiteSpace(roleId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var customRoles = await roleRepository.FindCustomRolesAsync(roleIds, cancellationToken);
        var overridesByRole = await roleRepository.ListPermissionOverridesAsync(roleIds, cancellationToken);
        foreach (var roleId in roleIds)
        {
            var rolePermissions = new HashSet<string>(StringComparer.Ordinal);
            if (ShortenLinkSystemRoles.PermissionBundles.TryGetValue(roleId, out var systemPermissions))
            {
                foreach (var permission in systemPermissions)
                    rolePermissions.Add(permission);
            }
            else
            {
                if (!customRoles.TryGetValue(roleId, out var customRole)
                    || !customRole.IsEnabled)
                {
                    continue;
                }

                foreach (var permission in customRole.Permissions)
                    rolePermissions.Add(permission);
            }

            ApplyPermissionOverrides(
                rolePermissions,
                overridesByRole.GetValueOrDefault(roleId, []));
            foreach (var permission in rolePermissions)
                permissions.Add(permission);
        }

        return new ShortenLinkUserSessionPrincipal(
            user.UserKey,
            user.Username,
            user.DisplayName,
            user.RoleIds,
            permissions.OrderBy(static permission => permission, StringComparer.Ordinal).ToList(),
            issuedAtUtc);
    }

    private static void ApplyPermissionOverrides(
        HashSet<string> permissions,
        IReadOnlyList<ShortenLinkRolePermissionOverride> overrides)
    {
        foreach (var item in overrides)
        {
            if (item.IsAllowed) permissions.Add(item.Permission);
            else permissions.Remove(item.Permission);
        }
    }
}
