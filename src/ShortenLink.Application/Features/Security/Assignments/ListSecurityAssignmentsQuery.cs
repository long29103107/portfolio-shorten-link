using ShortenLink.Application.Abstractions;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Features.Security.Assignments;

public sealed record ListSecurityAssignmentsQuery : IRequest<SecurityAssignmentsListResponse>;

internal sealed class ListSecurityAssignmentsQueryHandler(
    ICurrentRequestContext requestContext,
    IShortenLinkSecurityAssignmentRepository assignmentRepository)
    : IRequestHandler<ListSecurityAssignmentsQuery, SecurityAssignmentsListResponse>
{
    public async Task<SecurityAssignmentsListResponse> Handle(
        ListSecurityAssignmentsQuery request,
        CancellationToken cancellationToken)
    {
        await requestContext.EnsureAdminAsync(cancellationToken);
        var assignments = await assignmentRepository.ListAsync(cancellationToken);
        return new SecurityAssignmentsListResponse(
            assignments.Select(SecurityAssignmentResponse.FromDomain).ToList());
    }
}
