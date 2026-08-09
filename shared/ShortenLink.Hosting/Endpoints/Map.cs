using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using ShortenLink.Application.Contracts.Responses;
using ShortenLink.Application.Features.ShortLinks.Analytics;
using ShortenLink.Application.Features.ShortLinks.Bulk;
using ShortenLink.Application.Features.ShortLinks.Create;
using ShortenLink.Application.Features.ShortLinks.Delete;
using ShortenLink.Application.Features.ShortLinks.Details;
using ShortenLink.Application.Features.ShortLinks.Export;
using ShortenLink.Application.Features.ShortLinks.Expiration;
using ShortenLink.Application.Features.ShortLinks.List;
using ShortenLink.Application.Features.ShortLinks.Import;
using ShortenLink.Application.Features.ShortLinks.Shares;
using ShortenLink.Application.Features.ShortLinks.Status;
using ShortenLink.Application.Features.ShortLinks.Update;
using ShortenLink.Application.Features.ShortLinks.Redirect;
using ShortenLink.Core.Contracts.Requests;
using ShortenLink.Core.Services;
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
        group.AddEndpointFilter(new ShortenLinkExceptionEndpointFilter());
        ApplyPolicy(group, endpointOptions.AuthorizationPolicyName);

        group.MapGet("/", ListShortLinksAsync).WithName("ListShortenLinkEndpoints");
        var createEndpoint = group.MapPost("/", CreateShortLinkAsync).WithName("CreateShortenLinkEndpoint");
        group.MapPost("/import", ExecuteShortLinkImportAsync)
            .WithName("ExecuteShortenLinkImportEndpoint");
        group.MapPost("/import/dry-run", DryRunShortLinkImportAsync)
            .WithName("DryRunShortenLinkImportEndpoint");
        group.MapPost("/bulk", ExecuteShortLinkBulkOperationAsync)
            .WithName("ExecuteShortLinkBulkOperationEndpoint");
        group.MapGet("/export", ExportShortLinksAsync)
            .WithName("ExportShortenLinkEndpoint");
        group.MapPost("/expiration/execute", static async (
            ShortLinkExpirationExecutionRequestDto request,
            ISender sender,
            CancellationToken ct) =>
            Results.Ok(await sender.Send(
                new ExecuteShortLinkExpirationCommand(
                    request.EvaluatedAtUtc,
                    request.Limit,
                    request.RetainExpiredForSeconds,
                    request.ResumeFromCheckpoint),
                ct)))
            .WithName("ExecuteShortLinkExpirationEndpoint");
        group.MapGet("/{code}", static (string code, ISender sender, CancellationToken ct) =>
            sender.Send(new GetShortLinkDetailsQuery(code), ct));
        group.MapGet("/{code}/analytics", static (string code, int? limit, ISender sender, CancellationToken ct) =>
            sender.Send(new GetShortLinkAnalyticsQuery(code, limit), ct));
        group.MapGet("/{code}/shares", static (string code, ISender sender, CancellationToken ct) =>
            sender.Send(new ListShortLinkSharesQuery(code), ct));
        group.MapPut("/{code}/sharing-mode", static async (
            string code, ShortLinkSharingModeRequest request, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(
                new SetShortLinkSharingModeCommand(code, request.Mode), ct)));
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
            return await ResolveRedirectAsync(
                code,
                sender,
                options.Value,
                httpContext,
                GetRedirectPassword(httpContext),
                AcceptsHtml(httpContext.Request),
                cancellationToken);
        }).WithName("RedirectShortenLinkEndpoint");
        redirectEndpoint.AddEndpointFilter(new ShortenLinkExceptionEndpointFilter());

        var unlockEndpoint = endpoints.MapPost(pattern, static async (
            string code, ISender sender, IOptions<ShortenLinkOptions> options,
            HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var form = await httpContext.Request.ReadFormAsync(cancellationToken);
            return await ResolveRedirectAsync(
                code,
                sender,
                options.Value,
                httpContext,
                form["password"].ToString(),
                showPasswordPrompt: true,
                cancellationToken);
        }).WithName("UnlockShortenLinkEndpoint");
        unlockEndpoint.AddEndpointFilter(new ShortenLinkExceptionEndpointFilter());

        ApplyPolicy(redirectEndpoint, endpointOptions.AuthorizationPolicyName);
        ApplyPolicy(unlockEndpoint, endpointOptions.AuthorizationPolicyName);
        var rateOptions = endpoints.ServiceProvider.GetRequiredService<IOptions<ShortenLinkOptions>>().Value;
        if (rateOptions.RateLimiting.Enabled)
        {
            redirectEndpoint.RequireRateLimiting(ShortenLinkRateLimitingPolicyNames.Redirect);
            unlockEndpoint.RequireRateLimiting(ShortenLinkRateLimitingPolicyNames.Redirect);
        }
    }

    private static async Task<IResult> ResolveRedirectAsync(
        string code,
        ISender sender,
        ShortenLinkOptions options,
        HttpContext httpContext,
        string? password,
        bool showPasswordPrompt,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await sender.Send(new ResolveShortLinkCommand(
                code,
                httpContext.Connection.RemoteIpAddress?.ToString(),
                httpContext.Request.Headers.UserAgent.ToString(),
                httpContext.Request.Headers.Referer.ToString(),
                options.Redirect.EnableFrontendFallback,
                options.Redirect.FrontendFallbackPath,
                password,
                GetCountryCode(httpContext)), cancellationToken);
            return TypedResults.Redirect(response.Location);
        }
        catch (AuthenticationRequiredException exception)
            when (showPasswordPrompt && IsPasswordError(exception.ErrorCode))
        {
            return BuildPasswordPromptResult(
                httpContext,
                code,
                exception.ErrorCode == ShortLinkErrorCodes.InvalidLinkPassword);
        }
    }

    private static IResult BuildPasswordPromptResult(
        HttpContext httpContext,
        string code,
        bool invalidPassword)
    {
        httpContext.Response.Headers.CacheControl = "no-store";
        httpContext.Response.Headers.Pragma = "no-cache";

        var action = WebUtility.HtmlEncode($"{httpContext.Request.PathBase}{httpContext.Request.Path}");
        var encodedCode = WebUtility.HtmlEncode(code);
        var errorMarkup = invalidPassword
            ? "<p class=\"password-prompt-error\" role=\"alert\">That password is not correct. Try again.</p>"
            : string.Empty;
        var html = $$"""
            <!doctype html>
            <html lang="en">
              <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>Unlock short link</title>
                <style>
                  :root { color-scheme: dark; font-family: Segoe UI, Arial, sans-serif; background: #171717; color: #f5f5f5; }
                  body { min-height: 100vh; display: grid; place-items: center; margin: 0; padding: 24px; background: radial-gradient(circle at top, #2d211e, #171717 48%); }
                  main { width: min(430px, 100%); border: 1px solid #454545; border-radius: 12px; background: #242424; padding: 28px; box-shadow: 0 24px 70px #0008; }
                  .eyebrow { margin: 0 0 8px; color: #ff7a59; font-size: .75rem; font-weight: 800; letter-spacing: .12em; text-transform: uppercase; }
                  h1 { margin: 0; font-size: 1.65rem; }
                  p { color: #b7b7b7; line-height: 1.5; }
                  code { color: #f5f5f5; overflow-wrap: anywhere; }
                  label { display: grid; gap: 8px; margin-top: 22px; color: #ddd; font-weight: 700; }
                  input { width: 100%; box-sizing: border-box; border: 1px solid #555; border-radius: 8px; background: #303030; color: #fff; padding: 12px; font: inherit; }
                  input:focus { border-color: #ff7a59; outline: 3px solid #ee684c44; }
                  button { width: 100%; margin-top: 14px; border: 0; border-radius: 8px; background: #ee684c; color: #fff; padding: 12px; font: inherit; font-weight: 800; cursor: pointer; }
                  button:hover { background: #ff7a59; }
                  .password-prompt-error { color: #ff8a80; font-weight: 700; }
                </style>
              </head>
              <body>
                <main>
                  <p class="eyebrow">Protected short link</p>
                  <h1>Enter password to continue</h1>
                  <p>This short link <code>{{encodedCode}}</code> is password protected.</p>
                  {{errorMarkup}}
                  <form method="post" action="{{action}}">
                    <label for="password">Password
                      <input id="password" name="password" type="password" autocomplete="current-password" required autofocus>
                    </label>
                    <button type="submit">Continue</button>
                  </form>
                </main>
              </body>
            </html>
            """;

        return Results.Content(
            html,
            "text/html; charset=utf-8",
            Encoding.UTF8,
            StatusCodes.Status401Unauthorized);
    }

    private static bool IsPasswordError(string errorCode) =>
        errorCode is ShortLinkErrorCodes.PasswordRequired or ShortLinkErrorCodes.InvalidLinkPassword;

    private static bool AcceptsHtml(HttpRequest request) =>
        request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase);

    private static async Task<IResult> CreateShortLinkAsync(
        ShortLinkCreateRequest request, ISender sender, IOptions<ShortenLinkOptions> options,
        HttpContext httpContext, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateShortLinkCommand(
                request.OriginalUrl,
                request.ExpiredAtUtc,
                GetIdempotencyKey(httpContext),
                request.ActiveFromUtc,
                request.MaxClicks,
                request.Password,
                request.Folder,
                request.Tags), cancellationToken);
        var shortLink = result.ShortLink!;
        var response = ShortLinkCreatedResponse.FromDomain(
            shortLink, BuildShortUrl(shortLink.Code, options.Value, httpContext));
        return result.Replayed
            ? TypedResults.Ok(response)
            : TypedResults.Created($"/api/short-links/{shortLink.Code}", response);
    }

    private static Task<ShortLinkAdminListResponse> ListShortLinksAsync(
        [AsParameters] ShortLinkListEndpointRequest request,
        ISender sender, IOptions<ShortenLinkOptions> options,
        HttpContext httpContext, CancellationToken cancellationToken) =>
        sender.Send(new ListShortLinksQuery(
            GetBaseUrl(options.Value, httpContext),
            request),
            cancellationToken);

    private static Task<ShortLinkImportDryRunResponse> DryRunShortLinkImportAsync(
        ShortLinkImportRequest request,
        ISender sender,
        CancellationToken cancellationToken) =>
        sender.Send(new DryRunShortLinkImportCommand(request.Items), cancellationToken);

    private static Task<ShortLinkImportExecutionResponse> ExecuteShortLinkImportAsync(
        ShortLinkImportRequest request,
        ISender sender,
        CancellationToken cancellationToken) =>
        sender.Send(new ExecuteShortLinkImportCommand(request.Items), cancellationToken);

    private static Task<ShortLinkBulkOperationResponse> ExecuteShortLinkBulkOperationAsync(
        ShortLinkBulkOperationRequest request,
        ISender sender,
        CancellationToken cancellationToken) =>
        sender.Send(
            new ExecuteShortLinkBulkOperationCommand(
                request.Codes,
                request.Operation,
                request.Folder,
                request.Tags),
            cancellationToken);

    private static Task<IAsyncEnumerable<ShortLinkExportRecord>> ExportShortLinksAsync(
        int? limit,
        ISender sender,
        CancellationToken cancellationToken) =>
        sender.Send(new ExportShortLinksQuery(limit), cancellationToken);

    private static Task<ShortLinkAdminListItemResponse> UpdateShortLinkAsync(
        string code, ShortLinkUpdateRequest request, ISender sender, IOptions<ShortenLinkOptions> options,
        HttpContext httpContext, CancellationToken cancellationToken) =>
        sender.Send(new UpdateShortLinkCommand(
            code,
            request.OriginalUrl,
            request.ExpiredAtUtc,
            GetBaseUrl(options.Value, httpContext),
            request.ActiveFromUtc,
            request.MaxClicks,
            request.Password,
            request.ClearPassword,
            request.Folder,
            request.Tags), cancellationToken);

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

    private static string? GetIdempotencyKey(HttpContext httpContext) =>
        httpContext.Request.Headers.TryGetValue("Idempotency-Key", out var value)
            ? value.ToString()
            : null;

    private static string? GetRedirectPassword(HttpContext httpContext)
    {
        var header = httpContext.Request.Headers["X-Short-Link-Password"].ToString();
        return string.IsNullOrWhiteSpace(header)
            ? httpContext.Request.Query["password"].ToString()
            : header;
    }

    private static string? GetCountryCode(HttpContext httpContext)
    {
        var cloudflareCountry = httpContext.Request.Headers["CF-IPCountry"].FirstOrDefault();
        return string.IsNullOrWhiteSpace(cloudflareCountry)
            ? httpContext.Request.Headers["X-Country-Code"].FirstOrDefault()
            : cloudflareCountry;
    }

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
