using ShortenLink.Application.Features.Security.Roles;
using ShortenLink.Mediator;

namespace ShortenLink.Api.Endpoints;

internal static class SecurityRoleEndpoints
{
    public static IEndpointRouteBuilder MapSecurityRoleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/security/roles")
            .WithTags("Security Roles");

        group.MapGet("/", static (ISender sender, CancellationToken ct) =>
                sender.Send(new ListSecurityRolesQuery(), ct))
            .WithName("ListSecurityRoles");
        group.MapPut("/custom", static (SecurityCustomRoleUpsertRequest request, ISender sender, CancellationToken ct) =>
                sender.Send(new UpsertCustomSecurityRoleCommand(
                    request.Id, request.Name, request.Permissions, request.IsEnabled), ct))
            .WithName("UpsertCustomSecurityRole");
        group.MapPut("/{id}/permission-overrides", static (
                string id,
                SecurityRolePermissionOverridesRequest request,
                ISender sender,
                CancellationToken ct) =>
                sender.Send(new ReplaceSecurityRolePermissionOverridesCommand(id, request.Overrides), ct))
            .WithName("ReplaceSecurityRolePermissionOverrides");
        group.MapDelete("/custom/{id}", static (string id, ISender sender, CancellationToken ct) =>
                sender.Send(new DeleteCustomSecurityRoleCommand(id), ct))
            .WithName("DeleteCustomSecurityRole");

        return endpoints;
    }
}
