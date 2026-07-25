using ShortenLink.Application.Features.Security.Users;
using ShortenLink.Mediator;

namespace ShortenLink.Api.Endpoints;

internal static class SecurityUserEndpoints
{
    public static IEndpointRouteBuilder MapSecurityUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/security/users")
            .WithTags("Security Users");

        group.MapGet("/", static (ISender sender, CancellationToken ct) =>
                sender.Send(new ListSecurityUsersQuery(), ct))
            .WithName("ListSecurityUsers");
        group.MapPut("/", static (SecurityUserUpsertRequest request, ISender sender, CancellationToken ct) =>
                sender.Send(new UpsertSecurityUserCommand(
                    request.Id,
                    request.Username,
                    request.DisplayName,
                    request.Password,
                    request.RoleIds,
                    request.IsEnabled), ct))
            .WithName("UpsertSecurityUser");
        group.MapPost("/{id}/disable", static (string id, ISender sender, CancellationToken ct) =>
                sender.Send(new DisableSecurityUserCommand(id), ct))
            .WithName("DisableSecurityUser");

        return endpoints;
    }
}
