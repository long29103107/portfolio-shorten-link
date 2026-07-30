using ShortenLink.Application.Abstractions;
using ShortenLink.Application.Features.Audit;
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
    TimeProvider timeProvider,
    ShortLinkAuditWriter auditWriter)
    : IRequestHandler<UpsertSecurityAssignmentCommand, SecurityAssignmentResponse>
{
    public async Task<SecurityAssignmentResponse> Handle(
        UpsertSecurityAssignmentCommand request,
        CancellationToken cancellationToken)
    {
        var actor = await requestContext.AuthorizeAsync(
            SecurityFeatureSupport.AdminOnly,
            cancellationToken);
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

        var credentialKeyHash = ShortenLinkSecurityCredentialHasher.HashApiKey(
            request.CredentialKey);
        var existing = await assignmentRepository.FindByCredentialKeyHashAsync(
            credentialKeyHash,
            cancellationToken);
        var assignment = new ShortenLinkSecurityAssignment(
            credentialKeyHash,
            request.Name.Trim(),
            roles,
            permissions,
            request.IsEnabled ?? true,
            existing?.CreatedAt ?? timeProvider.GetUtcNow(),
            existing?.Id);
        await assignmentRepository.AddOrUpdateAsync(assignment, cancellationToken);
        await auditWriter.RecordAsync(
            actor,
            existing is null
                ? ShortLinkAuditActions.SecurityAssignmentCreated
                : ShortLinkAuditActions.SecurityAssignmentUpdated,
            assignment.Id.ToString("D"),
            ownerUserId: null,
            cancellationToken: cancellationToken,
            targetType: ShortLinkAuditTargetTypes.SecurityAssignment);
        return SecurityAssignmentResponse.FromDomain(assignment);
    }
}
