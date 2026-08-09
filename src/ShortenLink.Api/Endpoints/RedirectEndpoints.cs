using Microsoft.Extensions.Options;
using ShortenLink.Hosting;
using ShortenLink.Application.Features.ShortLinks.Redirect;
using ShortenLink.Mediator;

namespace ShortenLink.Api.Endpoints;

internal static class RedirectEndpoints
{
    public static IEndpointRouteBuilder MapRedirectEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var redirectEndpoint = endpoints.MapGet(
                "/{code}",
                static async (
                    string code,
                    ISender sender,
                    IOptions<ShortenLinkOptions> options,
                    HttpContext httpContext,
                    CancellationToken cancellationToken) =>
                {
                    var response = await sender.Send(
                        new ResolveShortLinkCommand(
                            code,
                            httpContext.Connection.RemoteIpAddress?.ToString(),
                            httpContext.Request.Headers.UserAgent.ToString(),
                            httpContext.Request.Headers.Referer.ToString(),
                            options.Value.Redirect.EnableFrontendFallback,
                            options.Value.Redirect.FrontendFallbackPath,
                            GetRedirectPassword(httpContext)),
                        cancellationToken);
                    return TypedResults.Redirect(response.Location);
                })
            .WithName("RedirectShortLink");

        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<ShortenLinkOptions>>().Value;
        if (options.RateLimiting.Enabled)
        {
            redirectEndpoint.RequireRateLimiting(ShortenLinkRateLimitingPolicyNames.Redirect);
        }

        return endpoints;
    }

    private static string? GetRedirectPassword(HttpContext httpContext)
    {
        var header = httpContext.Request.Headers["X-Short-Link-Password"].ToString();
        return string.IsNullOrWhiteSpace(header)
            ? httpContext.Request.Query["password"].ToString()
            : header;
    }
}
