namespace ShortenLink.Api.Endpoints;

internal static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/api/health", () => TypedResults.Ok(new HealthResponse("ok", "ShortenLink.Api")))
            .WithName("Health")
            .WithTags("Operations");

        return endpoints;
    }
}


public sealed record HealthResponse(string Status, string App);
