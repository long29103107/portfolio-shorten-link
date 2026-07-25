using ShortenLink.Application.Abstractions;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Features.Security.Assignments;

public sealed record DisableSecurityAssignmentCommand(
    string CredentialKeyHash) : IRequest<SecurityAssignmentDisabledResponse>;

internal sealed class DisableSecurityAssignmentCommandHandler(
    ICurrentRequestContext requestContext,
    IShortenLinkSecurityAssignmentRepository assignmentRepository)
    : IRequestHandler<DisableSecurityAssignmentCommand, SecurityAssignmentDisabledResponse>
{
    public async Task<SecurityAssignmentDisabledResponse> Handle(
        DisableSecurityAssignmentCommand request,
        CancellationToken cancellationToken)
    {
        await requestContext.EnsureAdminAsync(cancellationToken);
        if (request.CredentialKeyHash.Length != 64
            || request.CredentialKeyHash.Any(static value =>
                value is not (>= '0' and <= '9')
                    and not (>= 'a' and <= 'f')
                    and not (>= 'A' and <= 'F')))
            throw new RequestValidationException(ErrorCodes.InvalidCredentialHash, "Credential key hash is invalid.");
        if (!await assignmentRepository.DisableAsync(request.CredentialKeyHash, cancellationToken))
            throw new NotFoundException(ErrorCodes.NotFound, "Security assignment was not found.");
        return new SecurityAssignmentDisabledResponse(request.CredentialKeyHash, false);
    }
}
