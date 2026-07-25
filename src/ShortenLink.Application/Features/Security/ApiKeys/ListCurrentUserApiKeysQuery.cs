using ShortenLink.Application.Abstractions;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Features.Security.ApiKeys;

public sealed record ListCurrentUserApiKeysQuery : IRequest<SecurityUserApiKeysListResponse>;

internal sealed class ListCurrentUserApiKeysQueryHandler(
    ICurrentRequestContext requestContext,
    IShortenLinkUserApiKeyRepository apiKeyRepository)
    : IRequestHandler<ListCurrentUserApiKeysQuery, SecurityUserApiKeysListResponse>
{
    public async Task<SecurityUserApiKeysListResponse> Handle(
        ListCurrentUserApiKeysQuery request,
        CancellationToken cancellationToken)
    {
        var user = await GetRequiredUserAsync(cancellationToken);
        var apiKeys = await apiKeyRepository
            .ListByUserIdAsync(user.UserId, cancellationToken)
            ;

        return new SecurityUserApiKeysListResponse(
            apiKeys.Select(SecurityUserApiKeyResponse.FromDomain).ToList());
    }

    private async Task<CurrentUser> GetRequiredUserAsync(CancellationToken cancellationToken) =>
        await requestContext.GetCurrentUserAsync(cancellationToken)
        ?? throw new AuthenticationRequiredException();
}
