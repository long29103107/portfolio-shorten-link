using ShortenLink.Application.Abstractions;
using ShortenLink.Application.Features.Audit;
using ShortenLink.Core.Security;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Features.Security.Roles;

public sealed record UpsertCustomSecurityRoleCommand(
    string Id,
    string Name,
    IReadOnlyList<string>? Permissions,
    bool? IsEnabled) : IRequest<SecurityRoleResponse>;

internal sealed class UpsertCustomSecurityRoleCommandHandler(
    ICurrentRequestContext requestContext,
    IShortenLinkSecurityRoleRepository roleRepository,
    TimeProvider timeProvider,
    ShortLinkAuditWriter auditWriter)
    : IRequestHandler<UpsertCustomSecurityRoleCommand, SecurityRoleResponse>
{
    public async Task<SecurityRoleResponse> Handle(
        UpsertCustomSecurityRoleCommand request,
        CancellationToken cancellationToken)
    {
        var actor = await requestContext.AuthorizeAsync(
            SecurityFeatureSupport.AdminOnly,
            cancellationToken);
        if (ShortenLinkSystemRoles.PermissionBundles.ContainsKey(request.Id.Trim()))
            throw new BusinessRuleException(ErrorCodes.SystemRoleImmutable, "System roles cannot be created or updated through custom role APIs.");

        var permissions = SecurityFeatureSupport.NormalizeDistinct(request.Permissions);
        var roleId = request.Id.Trim();
        var existing = await roleRepository.FindCustomRoleAsync(roleId, cancellationToken);
        var role = new ShortenLinkCustomRole(
            roleId,
            request.Name.Trim(),
            permissions,
            request.IsEnabled ?? true,
            existing?.CreatedAt ?? timeProvider.GetUtcNow(),
            existing?.Id);
        await roleRepository.AddOrUpdateCustomRoleAsync(role, cancellationToken);
        var overrides = await roleRepository.ListPermissionOverridesAsync(role.RoleKey, cancellationToken);
        await auditWriter.RecordAsync(
            actor,
            existing is null
                ? ShortLinkAuditActions.SecurityRoleCreated
                : ShortLinkAuditActions.SecurityRoleUpdated,
            role.RoleKey,
            ownerUserId: null,
            cancellationToken: cancellationToken,
            targetType: ShortLinkAuditTargetTypes.SecurityRole);
        return SecurityRoleResponse.Custom(role, overrides);
    }
}
