using ShortenLink.Application.Features.MockData;
using ShortenLink.Mediator;

namespace ShortenLink.Api.Endpoints;

internal static class MockDataEndpoints
{
    public static IEndpointRouteBuilder MapMockDataEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost("/api/mock/seed-short-links", SeedShortLinksAsync)
            .WithName("SeedMockShortLinks")
            .WithTags("Mock Data");

        return endpoints;
    }

    private static async Task<IResult> SeedShortLinksAsync(
        ISender sender,
        int? count,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new SeedMockShortLinksCommand(count),
            cancellationToken);

        return TypedResults.Ok(result);
    }
}
