using ShortenLink.Application.Abstractions;
using ShortenLink.Core.Security;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Features.Security.Roles;

public sealed record DeleteCustomSecurityRoleCommand(
    string Id) : IRequest<SecurityRoleDeletedResponse>;

internal sealed class DeleteCustomSecurityRoleCommandHandler(
    ICurrentRequestContext requestContext,
    IShortenLinkSecurityRoleRepository roleRepository,
    IShortenLinkSecurityUserRepository userRepository)
    : IRequestHandler<DeleteCustomSecurityRoleCommand, SecurityRoleDeletedResponse>
{
    public async Task<SecurityRoleDeletedResponse> Handle(
        DeleteCustomSecurityRoleCommand request,
        CancellationToken cancellationToken)
    {
        await requestContext.EnsureAdminAsync(cancellationToken);
        if (ShortenLinkSystemRoles.PermissionBundles.ContainsKey(request.Id))
            throw new BusinessRuleException(ErrorCodes.SystemRoleImmutable, "System roles cannot be deleted.");
        if (await roleRepository.FindCustomRoleAsync(request.Id, cancellationToken) is null)
            throw new NotFoundException(ErrorCodes.NotFound, "Custom role was not found.");

        var users = await userRepository.ListAsync(includeHidden: true, cancellationToken);
        var assignedCount = users.Count(user =>
            user.RoleIds.Contains(request.Id, StringComparer.OrdinalIgnoreCase));
        if (assignedCount > 0)
            throw new ConflictException(
                ErrorCodes.RoleInUse,
                $"Role is assigned to {assignedCount} user(s). Remove or replace the role on those users before deleting it.");
        if (!await roleRepository.DeleteCustomRoleAsync(request.Id, cancellationToken))
            throw new NotFoundException(ErrorCodes.NotFound, "Custom role was not found.");
        return new SecurityRoleDeletedResponse(request.Id);
    }
}
