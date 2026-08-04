using ShortenLink.Application.Abstractions;
using ShortenLink.Application.Features.Audit;
using ShortenLink.Core.Security;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Features.Security.Users;

public sealed record UpsertSecurityUserCommand(
    string Id,
    string Username,
    string DisplayName,
    string? Password,
    IReadOnlyList<string>? RoleIds,
    bool? IsEnabled) : IRequest<SecurityUserResponse>;

internal sealed class UpsertSecurityUserCommandHandler(
    ICurrentRequestContext requestContext,
    IShortenLinkSecurityUserRepository userRepository,
    IShortenLinkSecurityRoleRepository roleRepository,
    TimeProvider timeProvider,
    ShortLinkAuditWriter auditWriter)
    : IRequestHandler<UpsertSecurityUserCommand, SecurityUserResponse>
{
    public async Task<SecurityUserResponse> Handle(
        UpsertSecurityUserCommand request,
        CancellationToken cancellationToken)
    {
        var actor = await requestContext.AuthorizeAsync(
            SecurityFeatureSupport.AdminOnly,
            cancellationToken);
        var id = request.Id.Trim();
        var existing = await userRepository.FindByIdAsync(id, cancellationToken);
        if (existing is { IsBootstrap: true })
            throw new BusinessRuleException(ErrorCodes.BootstrapUserImmutable, "The bootstrap admin user cannot be updated through user management APIs.");
        var usernameOwner = await userRepository.FindByUsernameAsync(request.Username.Trim(), cancellationToken);
        if (usernameOwner is not null && !usernameOwner.UserKey.Equals(id, StringComparison.Ordinal))
            throw SecurityFeatureSupport.Validation(ErrorCodes.InvalidSecurityUser, "Username is already assigned to another user.", "username");

        var roleIds = SecurityFeatureSupport.NormalizeDistinct(request.RoleIds);
        foreach (var roleId in roleIds)
        {
            if (ShortenLinkSystemRoles.PermissionBundles.ContainsKey(roleId))
                continue;
            if (await roleRepository.FindCustomRoleAsync(roleId, cancellationToken) is null)
                throw SecurityFeatureSupport.Validation(ErrorCodes.InvalidRole, $"Unknown role '{roleId}'.", "roleIds");
        }
        if (existing is null && roleIds.Count == 0)
            roleIds = [ShortenLinkSystemRoles.User];

        var passwordHash = !string.IsNullOrWhiteSpace(request.Password)
            ? ShortenLinkSecurityCredentialHasher.HashPassword(request.Password)
            : existing?.PasswordHash ?? ShortenLinkSecurityCredentialHasher.PasswordNotSetHash;
        var user = new ShortenLinkSecurityUser(
            id,
            request.Username.Trim(),
            request.DisplayName.Trim(),
            passwordHash,
            roleIds,
            request.IsEnabled ?? true,
            isHidden: false,
            isBootstrap: false,
            existing?.CreatedAt ?? timeProvider.GetUtcNow());
        await userRepository.AddOrUpdateAsync(user, cancellationToken);
        await auditWriter.RecordAsync(
            actor,
            existing is null
                ? ShortLinkAuditActions.SecurityUserCreated
                : ShortLinkAuditActions.SecurityUserUpdated,
            user.UserKey,
            ownerUserId: null,
            subjectUserId: user.UserKey,
            cancellationToken: cancellationToken,
            targetType: ShortLinkAuditTargetTypes.SecurityUser);
        return SecurityUserResponse.FromDomain(user);
    }
}
