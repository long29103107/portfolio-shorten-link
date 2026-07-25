using ShortenLink.Application.Abstractions;
using ShortenLink.Core.Security;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Features.Security.Roles;

public sealed record ReplaceSecurityRolePermissionOverridesCommand(
    string Id,
    IReadOnlyList<SecurityRolePermissionOverrideRequest>? Overrides)
    : IRequest<SecurityRoleResponse>;

internal sealed class ReplaceSecurityRolePermissionOverridesCommandHandler(
    ICurrentRequestContext requestContext,
    IShortenLinkSecurityRoleRepository roleRepository)
    : IRequestHandler<ReplaceSecurityRolePermissionOverridesCommand, SecurityRoleResponse>
{
    public async Task<SecurityRoleResponse> Handle(
        ReplaceSecurityRolePermissionOverridesCommand request,
        CancellationToken cancellationToken)
    {
        await requestContext.EnsureAdminAsync(cancellationToken);
        var roleId = request.Id.Trim();
        var isSystem = ShortenLinkSystemRoles.PermissionBundles.TryGetValue(roleId, out var defaults);
        var customRole = isSystem ? null : await roleRepository.FindCustomRoleAsync(roleId, cancellationToken);
        if (!isSystem && customRole is null)
            throw new NotFoundException(ErrorCodes.NotFound, "Security role was not found.");

        var normalized = new List<ShortenLinkRolePermissionOverride>();
        foreach (var item in request.Overrides ?? [])
        {
            if (!ShortenLinkPermissionCatalog.All.Contains(item.Permission))
                throw SecurityFeatureSupport.Validation(ErrorCodes.InvalidPermission, $"Unknown permission '{item.Permission}'.", "overrides");
            if (normalized.Any(existing => existing.Permission == item.Permission))
                throw SecurityFeatureSupport.Validation(ErrorCodes.DuplicatePermission, $"Permission '{item.Permission}' has more than one override.", "overrides");
            normalized.Add(ShortenLinkRolePermissionOverride.Create(item.Permission, item.IsAllowed));
        }

        await roleRepository.ReplacePermissionOverridesAsync(roleId, normalized, cancellationToken);
        return isSystem
            ? SecurityRoleResponse.System(roleId, defaults!, normalized)
            : SecurityRoleResponse.Custom(customRole!, normalized);
    }
}
