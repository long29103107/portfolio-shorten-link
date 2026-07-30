using ShortenLink.Application.Features.RateLimiting;
using ShortenLink.Mediator;

namespace ShortenLink.Api.Endpoints;

internal static class RateLimitEndpoints
{
    public static IEndpointRouteBuilder MapRateLimitEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(
                "/api/admin/rate-limits",
                static (ISender sender, CancellationToken cancellationToken) =>
                    sender.Send(new GetRateLimitActivityQuery(), cancellationToken))
            .WithTags("Operations")
            .WithName("GetRateLimitActivity");

        return endpoints;
    }
}
