using ShortenLink.Application.Abstractions;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Features.Security.Users;

public sealed record ListSecurityUsersQuery : IRequest<SecurityUsersListResponse>;

internal sealed class ListSecurityUsersQueryHandler(
    ICurrentRequestContext requestContext,
    IShortenLinkSecurityUserRepository userRepository)
    : IRequestHandler<ListSecurityUsersQuery, SecurityUsersListResponse>
{
    public async Task<SecurityUsersListResponse> Handle(
        ListSecurityUsersQuery request,
        CancellationToken cancellationToken)
    {
        await requestContext.EnsureAdminAsync(cancellationToken);
        var users = await userRepository.ListAsync(includeHidden: false, cancellationToken);
        return new SecurityUsersListResponse(
            users.Select(SecurityUserResponse.FromDomain).ToList());
    }
}
