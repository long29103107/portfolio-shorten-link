using ShortenLink.Application.Abstractions;
using ShortenLink.Application.Contracts.Responses;
using ShortenLink.Application.Features.Security;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Features.RateLimiting;

public sealed record GetRateLimitActivityQuery : IRequest<RateLimitActivityResponse>;

internal sealed class GetRateLimitActivityQueryHandler(
    ICurrentRequestContext requestContext,
    IRateLimitActivityReader activityReader)
    : IRequestHandler<GetRateLimitActivityQuery, RateLimitActivityResponse>
{
    public async Task<RateLimitActivityResponse> Handle(
        GetRateLimitActivityQuery request,
        CancellationToken cancellationToken)
    {
        await requestContext.EnsureAuthorizedAsync(
            SecurityFeatureSupport.AdminOnly,
            cancellationToken);

        return activityReader.GetSnapshot();
    }
}
