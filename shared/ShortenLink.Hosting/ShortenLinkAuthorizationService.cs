using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using ShortenLink.Core.Security;

namespace ShortenLink.Hosting;

public interface IShortenLinkAuthorizationService
{
    Task<ShortenLinkAuthorizationResult> AuthorizeAsync(
        HttpContext httpContext,
        string permission,
        CancellationToken cancellationToken = default);
}

public sealed record ShortenLinkAuthorizationResult(
    bool Succeeded,
    bool IsAuthenticated,
    string? ErrorCode,
    string? ErrorMessage,
    string? UserId,
    bool IsAdmin,
    string? ActorId)
{
    public static ShortenLinkAuthorizationResult Success(
        string? userId = null,
        bool isAdmin = true,
        string? actorId = null) =>
        new(
            true,
            true,
            null,
            null,
            userId,
            isAdmin,
            actorId ?? userId ?? (isAdmin ? "system:admin" : "system"));

    public static ShortenLinkAuthorizationResult Unauthorized() =>
        new(false, false, "unauthorized", "A valid credential is required.", null, false, null);

    public static ShortenLinkAuthorizationResult Forbidden() =>
        new(false, true, "forbidden", "The credential does not include the required permission.", null, false, null);
}

public sealed class ShortenLinkAuthorizationService(
    IOptions<ShortenLinkOptions> options,
    IShortenLinkSecurityAssignmentRepository securityAssignmentRepository,
    IShortenLinkUserApiKeyRepository userApiKeyRepository,
    IShortenLinkSecurityUserRepository userRepository,
    IShortenLinkSecurityRoleRepository roleRepository,
    IShortenLinkUserSessionService userSessionService) : IShortenLinkAuthorizationService
{
    public async Task<ShortenLinkAuthorizationResult> AuthorizeAsync(
        HttpContext httpContext,
        string permission,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);

        var security = options.Value.Security;
        if (HasBearerToken(httpContext))
        {
            var session = await userSessionService
                .GetCurrentUserAsync(httpContext, cancellationToken)
                ;
            if (!session.Succeeded || session.Principal is null)
            {
                return ShortenLinkAuthorizationResult.Unauthorized();
            }

            var isAdmin = session.Principal.Roles.Contains(
                ShortenLinkRoles.Admin,
                StringComparer.OrdinalIgnoreCase);
            return (permission == ShortenLinkPermissions.AdminOnly
                    ? isAdmin
                    : session.Principal.Permissions.Contains(permission, StringComparer.Ordinal))
                ? ShortenLinkAuthorizationResult.Success(
                    session.Principal.UserId,
                    isAdmin)
                : ShortenLinkAuthorizationResult.Forbidden();
        }

        if (!security.Enabled)
        {
            return ShortenLinkAuthorizationResult.Success(isAdmin: true);
        }

        if (!httpContext.Request.Headers.TryGetValue(security.HeaderName, out var keyValues))
        {
            return ShortenLinkAuthorizationResult.Unauthorized();
        }

        var apiKey = keyValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return ShortenLinkAuthorizationResult.Unauthorized();
        }

        var apiKeyHash = ShortenLinkSecurityCredentialHasher.HashApiKey(apiKey);
        var userApiKey = await userApiKeyRepository
            .FindByKeyHashAsync(apiKeyHash, cancellationToken)
            ;
        if (userApiKey is not null)
        {
            if (!userApiKey.IsEnabled)
            {
                return ShortenLinkAuthorizationResult.Unauthorized();
            }

            var owner = await userRepository
                .FindByIdAsync(userApiKey.UserId, cancellationToken)
                ;
            if (owner is null || !owner.IsEnabled)
            {
                return ShortenLinkAuthorizationResult.Unauthorized();
            }

            var userPrincipal = await userSessionService
                .CreatePrincipalAsync(owner, userApiKey.CreatedAt, cancellationToken)
                ;

            var isAdmin = userPrincipal.Roles.Contains(
                ShortenLinkRoles.Admin,
                StringComparer.OrdinalIgnoreCase);
            return (permission == ShortenLinkPermissions.AdminOnly
                    ? isAdmin
                    : userPrincipal.Permissions.Contains(permission, StringComparer.Ordinal))
                ? ShortenLinkAuthorizationResult.Success(
                    userPrincipal.UserId,
                    isAdmin)
                : ShortenLinkAuthorizationResult.Forbidden();
        }

        var persistedAssignment = await securityAssignmentRepository
            .FindByCredentialKeyHashAsync(apiKeyHash, cancellationToken)
            ;
        if (persistedAssignment is not null)
        {
            if (!persistedAssignment.IsEnabled)
            {
                return ShortenLinkAuthorizationResult.Unauthorized();
            }

            var persistedPermissions = await GetEffectivePermissionsAsync(
                persistedAssignment.Roles,
                persistedAssignment.Permissions,
                cancellationToken);
            var isAdmin = persistedAssignment.Roles.Contains(
                ShortenLinkRoles.Admin,
                StringComparer.OrdinalIgnoreCase);

            return (permission == ShortenLinkPermissions.AdminOnly
                    ? isAdmin
                    : persistedPermissions.Contains(permission))
                ? ShortenLinkAuthorizationResult.Success(
                    isAdmin: isAdmin,
                    actorId: $"assignment:{persistedAssignment.Id:D}")
                : ShortenLinkAuthorizationResult.Forbidden();
        }

        var principal = security.ApiKeys.FirstOrDefault(candidate =>
            !string.IsNullOrWhiteSpace(candidate.Key)
            && string.Equals(candidate.Key, apiKey, StringComparison.Ordinal));
        if (principal is null)
        {
            return ShortenLinkAuthorizationResult.Unauthorized();
        }

        var permissions = await GetEffectivePermissionsAsync(
            principal.Roles,
            principal.Permissions,
            cancellationToken);
        var configuredIsAdmin = principal.Roles.Contains(
            ShortenLinkRoles.Admin,
            StringComparer.OrdinalIgnoreCase);
        return (permission == ShortenLinkPermissions.AdminOnly
                ? configuredIsAdmin
                : permissions.Contains(permission))
            ? ShortenLinkAuthorizationResult.Success(
                isAdmin: configuredIsAdmin,
                actorId: $"configured:{NormalizeActorName(principal.Name)}")
            : ShortenLinkAuthorizationResult.Forbidden();
    }

    private async Task<HashSet<string>> GetEffectivePermissionsAsync(
        IEnumerable<string> roles,
        IEnumerable<string> explicitPermissions,
        CancellationToken cancellationToken)
    {
        var permissions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var permission in explicitPermissions.Where(static permission => !string.IsNullOrWhiteSpace(permission)))
        {
            permissions.Add(permission);
        }

        var roleIds = roles
            .Where(static role => !string.IsNullOrWhiteSpace(role))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var customRoles = await roleRepository.FindCustomRolesAsync(roleIds, cancellationToken);
        var overridesByRole = await roleRepository.ListPermissionOverridesAsync(roleIds, cancellationToken);

        foreach (var role in roleIds)
        {
            var rolePermissions = new HashSet<string>(StringComparer.Ordinal);
            if (ShortenLinkRoles.PermissionBundles.TryGetValue(role, out var systemPermissions))
            {
                rolePermissions.UnionWith(systemPermissions);
            }
            else
            {
                if (!customRoles.TryGetValue(role, out var customRole)
                    || !customRole.IsEnabled)
                {
                    continue;
                }
                rolePermissions.UnionWith(customRole.Permissions);
            }

            var overrides = overridesByRole.GetValueOrDefault(role, []);
            foreach (var item in overrides)
            {
                if (item.IsAllowed) rolePermissions.Add(item.Permission);
                else rolePermissions.Remove(item.Permission);
            }

            permissions.UnionWith(rolePermissions);
        }

        return permissions;
    }

    private static bool HasBearerToken(HttpContext httpContext)
    {
        var authorization = httpContext.Request.Headers.Authorization.FirstOrDefault();
        return !string.IsNullOrWhiteSpace(authorization)
            && authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeActorName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "api-key" : name.Trim();
}
