using ShortenLink.Mediator;
using ShortenLink.Core.Services;

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
    TimeProvider timeProvider)
    : IRequestHandler<ResolveShortLinkCommand, ResolveShortLinkResponse>
{
    public async Task<ResolveShortLinkResponse> Handle(
        ResolveShortLinkCommand request,
        CancellationToken cancellationToken)
    {
        var result = await shortLinkService.ResolveAsync(request.Code, cancellationToken);
        if (result.Succeeded && result.ShortLink is not null)
        {
            await clickRecorder.RecordAsync(
                new RecordShortLinkClickRequest(
                    result.ShortLink.Code,
                    timeProvider.GetUtcNow(),
                    request.RemoteIpAddress,
                    request.UserAgent,
                    request.Referrer),
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
