using ShortenLink.Application.Abstractions;
using ShortenLink.Application.Features.Audit;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Features.Security.Assignments;

public sealed record DisableSecurityAssignmentCommand(
    string CredentialKeyHash) : IRequest<SecurityAssignmentDisabledResponse>;

internal sealed class DisableSecurityAssignmentCommandHandler(
    ICurrentRequestContext requestContext,
    IShortenLinkSecurityAssignmentRepository assignmentRepository,
    ShortLinkAuditWriter auditWriter)
    : IRequestHandler<DisableSecurityAssignmentCommand, SecurityAssignmentDisabledResponse>
{
    public async Task<SecurityAssignmentDisabledResponse> Handle(
        DisableSecurityAssignmentCommand request,
        CancellationToken cancellationToken)
    {
        var actor = await requestContext.AuthorizeAsync(
            SecurityFeatureSupport.AdminOnly,
            cancellationToken);
        var existing = await assignmentRepository.FindByCredentialKeyHashAsync(
            request.CredentialKeyHash,
            cancellationToken);
        if (existing is null)
            throw new NotFoundException(ErrorCodes.NotFound, "Security assignment was not found.");
        if (!await assignmentRepository.DisableAsync(request.CredentialKeyHash, cancellationToken))
            throw new NotFoundException(ErrorCodes.NotFound, "Security assignment was not found.");
        await auditWriter.RecordAsync(
            actor,
            ShortLinkAuditActions.SecurityAssignmentDisabled,
            existing.Id.ToString("D"),
            ownerUserId: null,
            cancellationToken: cancellationToken,
            targetType: ShortLinkAuditTargetTypes.SecurityAssignment);
        return new SecurityAssignmentDisabledResponse(request.CredentialKeyHash, false);
    }
}
