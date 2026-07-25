using ShortenLink.Application.Features.Security.Sessions;
using ShortenLink.Mediator;

namespace ShortenLink.Api.Endpoints;

internal static class SecuritySessionEndpoints
{
    public static IEndpointRouteBuilder MapSecuritySessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/security")
            .WithTags("Security");

        group.MapPost(
                "/login",
                static (
                    SecurityLoginRequest request,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                    sender.Send(
                        new LoginSecurityUserCommand(
                            request.Email,
                            request.Username,
                            request.Password),
                        cancellationToken))
            .WithName("LoginSecurityUser");
        group.MapPost(
                "/refresh",
                static (
                    SecurityRefreshRequest request,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                    sender.Send(
                        new RefreshSecurityUserCommand(request.RefreshToken),
                        cancellationToken))
            .WithName("RefreshSecurityUser");
        group.MapGet(
                "/me",
                static (ISender sender, CancellationToken cancellationToken) =>
                    sender.Send(new GetCurrentSecurityUserQuery(), cancellationToken))
            .WithName("GetCurrentSecurityUser");

        return endpoints;
    }
}
