using ShortenLink.Application.Abstractions;
using ShortenLink.Core.Security;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Features.Security.Assignments;

public sealed record UpsertSecurityAssignmentCommand(
    string Name,
    string CredentialKey,
    IReadOnlyList<string>? Roles,
    IReadOnlyList<string>? Permissions,
    bool? IsEnabled) : IRequest<SecurityAssignmentResponse>;

internal sealed class UpsertSecurityAssignmentCommandHandler(
    ICurrentRequestContext requestContext,
    IShortenLinkSecurityAssignmentRepository assignmentRepository,
    TimeProvider timeProvider)
    : IRequestHandler<UpsertSecurityAssignmentCommand, SecurityAssignmentResponse>
{
    public async Task<SecurityAssignmentResponse> Handle(
        UpsertSecurityAssignmentCommand request,
        CancellationToken cancellationToken)
    {
        await requestContext.EnsureAdminAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(request.CredentialKey))
            throw SecurityFeatureSupport.Validation(ErrorCodes.InvalidSecurityAssignment, "Credential key is required.", "credentialKey");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw SecurityFeatureSupport.Validation(ErrorCodes.InvalidSecurityAssignment, "Assignment name is required.", "name");

        var roles = SecurityFeatureSupport.NormalizeDistinct(request.Roles);
        var unknownRole = roles.FirstOrDefault(role => !ShortenLinkSystemRoles.PermissionBundles.ContainsKey(role));
        if (unknownRole is not null)
            throw SecurityFeatureSupport.Validation(ErrorCodes.InvalidRole, $"Unknown system role '{unknownRole}'.", "roles");
        var permissions = SecurityFeatureSupport.NormalizeDistinct(request.Permissions);
        var unknownPermission = permissions.FirstOrDefault(permission => !ShortenLinkPermissionCatalog.All.Contains(permission));
        if (unknownPermission is not null)
            throw SecurityFeatureSupport.Validation(ErrorCodes.InvalidPermission, $"Unknown permission '{unknownPermission}'.", "permissions");

        var assignment = new ShortenLinkSecurityAssignment(
            ShortenLinkSecurityCredentialHasher.HashApiKey(request.CredentialKey),
            request.Name.Trim(),
            roles,
            permissions,
            request.IsEnabled ?? true,
            timeProvider.GetUtcNow());
        await assignmentRepository.AddOrUpdateAsync(assignment, cancellationToken);
        return SecurityAssignmentResponse.FromDomain(assignment);
    }
}
