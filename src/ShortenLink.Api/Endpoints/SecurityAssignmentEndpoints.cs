using ShortenLink.Application.Features.Security.Assignments;
using ShortenLink.Mediator;

namespace ShortenLink.Api.Endpoints;

internal static class SecurityAssignmentEndpoints
{
    public static IEndpointRouteBuilder MapSecurityAssignmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/security/assignments")
            .WithTags("Security Assignments");

        group.MapGet("/", static (ISender sender, CancellationToken ct) =>
                sender.Send(new ListSecurityAssignmentsQuery(), ct))
            .WithName("ListSecurityAssignments");
        group.MapPut("/", static (SecurityAssignmentUpsertRequest request, ISender sender, CancellationToken ct) =>
                sender.Send(new UpsertSecurityAssignmentCommand(
                    request.Name,
                    request.CredentialKey,
                    request.Roles,
                    request.Permissions,
                    request.IsEnabled), ct))
            .WithName("UpsertSecurityAssignment");
        group.MapPost("/{credentialKeyHash}/disable", static (
                string credentialKeyHash,
                ISender sender,
                CancellationToken ct) =>
                sender.Send(new DisableSecurityAssignmentCommand(credentialKeyHash), ct))
            .WithName("DisableSecurityAssignment");

        return endpoints;
    }
}
