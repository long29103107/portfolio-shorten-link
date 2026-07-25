using ShortenLink.Application.Abstractions;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Features.Security.Sessions;

public sealed record GetCurrentSecurityUserQuery : IRequest<SecurityCurrentUserResponse>;

internal sealed class GetCurrentSecurityUserQueryHandler(
    ICurrentRequestContext requestContext,
    TimeProvider timeProvider)
    : IRequestHandler<GetCurrentSecurityUserQuery, SecurityCurrentUserResponse>
{
    public async Task<SecurityCurrentUserResponse> Handle(
        GetCurrentSecurityUserQuery request,
        CancellationToken cancellationToken)
    {
        var user = await requestContext.GetCurrentUserAsync(cancellationToken)
            ?? throw new AuthenticationRequiredException();

        return new SecurityCurrentUserResponse(
            user.UserId,
            user.Username,
            user.DisplayName,
            user.Roles,
            user.Permissions,
            timeProvider.GetUtcNow());
    }
}
