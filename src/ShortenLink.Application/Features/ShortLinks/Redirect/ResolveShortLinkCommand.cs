using ShortenLink.Mediator;
using ShortenLink.Application.Abstractions;
using ShortenLink.Core.Abstractions;
using ShortenLink.Core.Services;
using CoreResolveShortLinkResponse = ShortenLink.Core.Contracts.Responses.ResolveShortLinkResponse;

namespace ShortenLink.Application.Features.ShortLinks.Redirect;

public sealed record ResolveShortLinkCommand(
    string Code,
    string? RemoteIpAddress,
    string? UserAgent,
    string? Referrer,
    bool EnableFallback,
    string? FallbackPath) : IRequest<ResolveShortLinkResponse>;

public sealed record ResolveShortLinkResponse(string Location);

internal sealed class ResolveShortLinkCommandHandler(
    IShortLinkService shortLinkService,
    IShortLinkClickRecorder clickRecorder,
    TimeProvider timeProvider,
    ICurrentRequestContext requestContext)
    : IRequestHandler<ResolveShortLinkCommand, ResolveShortLinkResponse>
{
    public async Task<ResolveShortLinkResponse> Handle(
        ResolveShortLinkCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = await requestContext.GetCurrentTenantIdAsync(cancellationToken);
        var result = tenantId is null
            ? await shortLinkService.ResolveAsync(request.Code, cancellationToken)
            : shortLinkService is ITenantAwareShortLinkService tenantAwareService
                ? await tenantAwareService.ResolveAsync(request.Code, cancellationToken, tenantId)
                : CoreResolveShortLinkResponse.Failure(
                    ShortLinkErrorCodes.TenantNotSupported,
                    "The configured resolve provider does not support tenant partitions.");
        if (result.Succeeded && result.ShortLink is not null)
        {
            await clickRecorder.RecordAsync(
                new RecordShortLinkClickRequest(
                    result.ShortLink.Code,
                    timeProvider.GetUtcNow(),
                    request.RemoteIpAddress,
                    request.UserAgent,
                    request.Referrer,
                    result.ShortLink.TenantId),
                cancellationToken);
            return new ResolveShortLinkResponse(result.ShortLink.OriginalUrl.AbsoluteUri);
        }

        if (request.EnableFallback
            && result.ErrorCode is ShortLinkErrorCodes.NotFound
                or ShortLinkErrorCodes.Expired
                or ShortLinkErrorCodes.Inactive)
        {
            return new ResolveShortLinkResponse(
                string.IsNullOrWhiteSpace(request.FallbackPath)
                    ? "/not-found"
                    : request.FallbackPath);
        }

        throw ShortLinkFeatureSupport.CreateException(result.ErrorCode, result.ErrorMessage);
    }
}
