using ShortenLink.Application.Abstractions;
using ShortenLink.Core.Security;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Features.Security.Roles;

public sealed record ListSecurityRolesQuery : IRequest<SecurityRolesListResponse>;

internal sealed class ListSecurityRolesQueryHandler(
    ICurrentRequestContext requestContext,
    IShortenLinkSecurityRoleRepository roleRepository)
    : IRequestHandler<ListSecurityRolesQuery, SecurityRolesListResponse>
{
    public async Task<SecurityRolesListResponse> Handle(
        ListSecurityRolesQuery request,
        CancellationToken cancellationToken)
    {
        await requestContext.EnsureAdminAsync(cancellationToken);
        var systemRoles = new List<SecurityRoleResponse>();
        foreach (var role in ShortenLinkSystemRoles.PermissionBundles
                     .OrderBy(static role => role.Key, StringComparer.OrdinalIgnoreCase))
        {
            var overrides = await roleRepository.ListPermissionOverridesAsync(role.Key, cancellationToken);
            systemRoles.Add(SecurityRoleResponse.System(role.Key, role.Value, overrides));
        }

        var customRoles = new List<SecurityRoleResponse>();
        foreach (var role in await roleRepository.ListCustomRolesAsync(cancellationToken))
        {
            var overrides = await roleRepository.ListPermissionOverridesAsync(role.RoleKey, cancellationToken);
            customRoles.Add(SecurityRoleResponse.Custom(role, overrides));
        }

        return new SecurityRolesListResponse(systemRoles, customRoles);
    }
}
