using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using ShortenLink.Hosting;
using ShortenLink.Application.Features.ShortLinks.Create;
using ShortenLink.Application.Features.ShortLinks.Analytics;
using ShortenLink.Application.Features.ShortLinks.Delete;
using ShortenLink.Application.Features.ShortLinks.Details;
using ShortenLink.Application.Features.ShortLinks.List;
using ShortenLink.Application.Features.ShortLinks.Shares;
using ShortenLink.Application.Features.ShortLinks.Status;
using ShortenLink.Application.Features.ShortLinks.Update;
using ShortenLink.Mediator;

namespace ShortenLink.Api.Endpoints;

internal static class ShortLinkManagementEndpoints
{
    public static IEndpointRouteBuilder MapShortLinkManagementEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/short-links")
            .WithTags("Short Links");

        group.MapGet("/", ListShortLinksAsync)
            .WithName("ListShortLinks");

        var createEndpoint = group.MapPost("/", CreateShortLinkAsync)
            .WithName("CreateShortLink");

        group.MapGet("/{code}", static (string code, ISender sender, CancellationToken ct) =>
                sender.Send(new GetShortLinkDetailsQuery(code), ct))
            .WithName("GetShortLinkDetails");
        group.MapGet("/{code}/analytics", static (
                string code, int? limit, ISender sender, CancellationToken ct) =>
                sender.Send(new GetShortLinkAnalyticsQuery(code, limit), ct))
            .WithName("GetShortLinkAnalytics");
        group.MapGet("/{code}/shares", static (string code, ISender sender, CancellationToken ct) =>
                sender.Send(new ListShortLinkSharesQuery(code), ct))
            .WithName("ListShortLinkShares");
        group.MapPut("/{code}/shares", static (
                string code,
                ShortLinkShareUpsertRequest request,
                ISender sender,
                CancellationToken ct) =>
                sender.Send(new UpsertShortLinkShareCommand(code, request.Username, request.Access), ct))
            .WithName("UpsertShortLinkShare");
        group.MapDelete("/{code}/shares/{userId}", DeleteShortLinkShareAsync)
            .WithName("DeleteShortLinkShare");
        group.MapPut("/{code}", UpdateShortLinkAsync)
            .WithName("UpdateShortLink");
        group.MapPost("/{code}/deactivate", static (string code, ISender sender, CancellationToken ct) =>
                sender.Send(new DeactivateShortLinkCommand(code), ct))
            .WithName("DeactivateShortLink");
        group.MapPost("/{code}/activate", static (string code, ISender sender, CancellationToken ct) =>
                sender.Send(new ActivateShortLinkCommand(code), ct))
            .WithName("ActivateShortLink");
        group.MapDelete("/{code}", static (string code, ISender sender, CancellationToken ct) =>
                sender.Send(new DeleteShortLinkCommand(code), ct))
            .WithName("DeleteShortLink");

        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<ShortenLinkOptions>>().Value;
        if (options.RateLimiting.Enabled)
        {
            createEndpoint.RequireRateLimiting(ShortenLinkRateLimitingPolicyNames.Create);
        }

        return endpoints;
    }

    private static async Task<IResult> CreateShortLinkAsync(
        ShortLinkCreateRequest request,
        ISender sender,
        IOptions<ShortenLinkOptions> options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var shortLink = await sender.Send(
            new CreateShortLinkCommand(
                request.OriginalUrl,
                request.ExpiredAtUtc),
            cancellationToken);

        var response = ShortLinkCreatedResponse.FromDomain(
            shortLink,
            BuildShortUrl(shortLink.Code, options.Value, httpContext));

        return TypedResults.Created($"/api/short-links/{shortLink.Code}", response);
    }

    private static Task<ShortLinkAdminListResponse> ListShortLinksAsync(
        [AsParameters] ShortLinkListEndpointRequest request,
        ISender sender,
        IOptions<ShortenLinkOptions> options,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        sender.Send(
            CreateListQuery(GetBaseUrl(options.Value, httpContext), request.Limit, request.Page, request.Cursor,
                request.Search, request.Status, request.SortBy, request.SortDirection, request.Fe, request.Sort),
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
        string code,
        ShortLinkUpdateRequest request,
        ISender sender,
        IOptions<ShortenLinkOptions> options,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        sender.Send(
            new UpdateShortLinkCommand(
                code,
                request.OriginalUrl,
                request.ExpiredAtUtc,
                GetBaseUrl(options.Value, httpContext)),
            cancellationToken);

    private static async Task<IResult> DeleteShortLinkShareAsync(
        string code,
        string userId,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteShortLinkShareCommand(code, userId), cancellationToken);
        return TypedResults.NoContent();
    }

    private static string BuildShortUrl(
        string code,
        ShortenLinkOptions options,
        HttpContext httpContext) =>
        new Uri(new Uri(GetBaseUrl(options, httpContext), UriKind.Absolute), code).AbsoluteUri;

    private static string GetBaseUrl(
        ShortenLinkOptions options,
        HttpContext httpContext) =>
        Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var configured)
            ? configured.AbsoluteUri
            : $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/";
}
