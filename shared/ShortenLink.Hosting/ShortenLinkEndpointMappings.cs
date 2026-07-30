using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using ShortenLink.Application.Contracts.Responses;
using ShortenLink.Application.Features.ShortLinks.Analytics;
using ShortenLink.Application.Features.ShortLinks.Create;
using ShortenLink.Application.Features.ShortLinks.Delete;
using ShortenLink.Application.Features.ShortLinks.Details;
using ShortenLink.Application.Features.ShortLinks.List;
using ShortenLink.Application.Features.ShortLinks.Shares;
using ShortenLink.Application.Features.ShortLinks.Status;
using ShortenLink.Application.Features.ShortLinks.Update;
using ShortenLink.Application.Features.ShortLinks.Redirect;
using ShortenLink.Core.Contracts.Requests;
using ShortenLink.Mediator;

namespace ShortenLink.Hosting;

public static class ShortenLinkEndpointMappings
{
    public static IEndpointRouteBuilder MapShortenLinkEndpoints(
        this IEndpointRouteBuilder endpoints,
        Action<ShortenLinkEndpointOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var options = new ShortenLinkEndpointOptions();
        configure?.Invoke(options);
        Validate(options);

        if (options.MapManagementEndpoints)
            MapManagementEndpoints(endpoints, options);
        if (options.MapRedirectEndpoint)
            MapRedirectEndpoint(endpoints, options);
        return endpoints;
    }

    private static void MapManagementEndpoints(IEndpointRouteBuilder endpoints, ShortenLinkEndpointOptions endpointOptions)
    {
        var group = endpoints.MapGroup(NormalizePrefix(endpointOptions.ManagementRoutePrefix))
            .WithTags("Short Links");
        ApplyPolicy(group, endpointOptions.AuthorizationPolicyName);

        group.MapGet("/", ListShortLinksAsync).WithName("ListShortenLinkEndpoints");
        var createEndpoint = group.MapPost("/", CreateShortLinkAsync).WithName("CreateShortenLinkEndpoint");
        group.MapGet("/{code}", static (string code, ISender sender, CancellationToken ct) =>
            sender.Send(new GetShortLinkDetailsQuery(code), ct));
        group.MapGet("/{code}/analytics", static (string code, int? limit, ISender sender, CancellationToken ct) =>
            sender.Send(new GetShortLinkAnalyticsQuery(code, limit), ct));
        group.MapGet("/{code}/shares", static (string code, ISender sender, CancellationToken ct) =>
            sender.Send(new ListShortLinkSharesQuery(code), ct));
        group.MapPut("/{code}/shares", static (
            string code, ShortLinkShareUpsertRequest request, ISender sender, CancellationToken ct) =>
            sender.Send(new UpsertShortLinkShareCommand(code, request.Username, request.Access), ct));
        group.MapDelete("/{code}/shares/{userId}", DeleteShortLinkShareAsync);
        group.MapPut("/{code}", UpdateShortLinkAsync);
        group.MapPost("/{code}/deactivate", static (string code, ISender sender, CancellationToken ct) =>
            sender.Send(new DeactivateShortLinkCommand(code), ct));
        group.MapPost("/{code}/activate", static (string code, ISender sender, CancellationToken ct) =>
            sender.Send(new ActivateShortLinkCommand(code), ct));
        group.MapDelete("/{code}", static (string code, ISender sender, CancellationToken ct) =>
            sender.Send(new DeleteShortLinkCommand(code), ct));

        var rateOptions = endpoints.ServiceProvider.GetRequiredService<IOptions<ShortenLinkOptions>>().Value;
        if (rateOptions.RateLimiting.Enabled)
            createEndpoint.RequireRateLimiting(ShortenLinkRateLimitingPolicyNames.Create);
    }

    private static void MapRedirectEndpoint(IEndpointRouteBuilder endpoints, ShortenLinkEndpointOptions endpointOptions)
    {
        var pattern = $"{NormalizePrefix(endpointOptions.RedirectRoutePrefix)}/{{code}}";
        var redirectEndpoint = endpoints.MapGet(pattern, static async (
            string code, ISender sender, IOptions<ShortenLinkOptions> options,
            HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var response = await sender.Send(new ResolveShortLinkCommand(
                code,
                httpContext.Connection.RemoteIpAddress?.ToString(),
                httpContext.Request.Headers.UserAgent.ToString(),
                httpContext.Request.Headers.Referer.ToString(),
                options.Value.Redirect.EnableFrontendFallback,
                options.Value.Redirect.FrontendFallbackPath), cancellationToken);
            return TypedResults.Redirect(response.Location);
        }).WithName("RedirectShortenLinkEndpoint");

        ApplyPolicy(redirectEndpoint, endpointOptions.AuthorizationPolicyName);
        var rateOptions = endpoints.ServiceProvider.GetRequiredService<IOptions<ShortenLinkOptions>>().Value;
        if (rateOptions.RateLimiting.Enabled)
            redirectEndpoint.RequireRateLimiting(ShortenLinkRateLimitingPolicyNames.Redirect);
    }

    private static async Task<IResult> CreateShortLinkAsync(
        ShortLinkCreateRequest request, ISender sender, IOptions<ShortenLinkOptions> options,
        HttpContext httpContext, CancellationToken cancellationToken)
    {
        var shortLink = await sender.Send(
            new CreateShortLinkCommand(request.OriginalUrl, request.ExpiredAtUtc), cancellationToken);
        var response = ShortLinkCreatedResponse.FromDomain(
            shortLink, BuildShortUrl(shortLink.Code, options.Value, httpContext));
        return TypedResults.Created($"/api/short-links/{shortLink.Code}", response);
    }

    private static Task<ShortLinkAdminListResponse> ListShortLinksAsync(
        int? limit, int? page, string? cursor, string? search, string? status,
        string? sortBy, string? sortDirection, string? fe, string? sort,
        ISender sender, IOptions<ShortenLinkOptions> options,
        HttpContext httpContext, CancellationToken cancellationToken) =>
        sender.Send(CreateListQuery(
            GetBaseUrl(options.Value, httpContext), limit, page, cursor,
            search, status, sortBy, sortDirection, fe, sort),
            cancellationToken);

    private static ListShortLinksQuery CreateListQuery(
        string baseUrl, int? limit, int? page, string? cursor,
        string? search, string? status, string? sortBy, string? sortDirection,
        string? filter, string? sort)
    {
        if (string.IsNullOrWhiteSpace(filter) && string.IsNullOrWhiteSpace(sort))
            return new ListShortLinksQuery(baseUrl, limit, page, cursor, search, status, sortBy, sortDirection);

        var parsed = ShortLinkListQueryParameterParser.Parse(filter, sort);
        return new ListShortLinksQuery(
            baseUrl, limit, page, cursor, parsed.Search, parsed.Status, parsed.SortBy, parsed.SortDirection);
    }

    private static Task<ShortLinkAdminListItemResponse> UpdateShortLinkAsync(
        string code, ShortLinkUpdateRequest request, ISender sender, IOptions<ShortenLinkOptions> options,
        HttpContext httpContext, CancellationToken cancellationToken) =>
        sender.Send(new UpdateShortLinkCommand(
            code, request.OriginalUrl, request.ExpiredAtUtc, GetBaseUrl(options.Value, httpContext)), cancellationToken);

    private static async Task<IResult> DeleteShortLinkShareAsync(
        string code, string userId, ISender sender, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteShortLinkShareCommand(code, userId), cancellationToken);
        return TypedResults.NoContent();
    }

    private static string BuildShortUrl(string code, ShortenLinkOptions options, HttpContext httpContext) =>
        new Uri(new Uri(GetBaseUrl(options, httpContext), UriKind.Absolute), code).AbsoluteUri;

    private static string GetBaseUrl(ShortenLinkOptions options, HttpContext httpContext) =>
        Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var configured)
            ? configured.AbsoluteUri
            : $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/";

    private static string NormalizePrefix(string value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : "/" + value.Trim().Trim('/');

    private static void ApplyPolicy(IEndpointConventionBuilder endpoint, string? policyName)
    {
        if (!string.IsNullOrWhiteSpace(policyName))
            endpoint.RequireAuthorization(policyName);
    }

    private static void Validate(ShortenLinkEndpointOptions options)
    {
        if (options.MapManagementEndpoints && string.IsNullOrWhiteSpace(options.ManagementRoutePrefix))
            throw new ArgumentException("ManagementRoutePrefix is required when management endpoints are enabled.", nameof(options));
    }
}
