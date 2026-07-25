using ShortenLink.Application.Abstractions;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Features.Security.Users;

public sealed record DisableSecurityUserCommand(
    string Id) : IRequest<SecurityUserDisabledResponse>;

internal sealed class DisableSecurityUserCommandHandler(
    ICurrentRequestContext requestContext,
    IShortenLinkSecurityUserRepository userRepository)
    : IRequestHandler<DisableSecurityUserCommand, SecurityUserDisabledResponse>
{
    public async Task<SecurityUserDisabledResponse> Handle(
        DisableSecurityUserCommand request,
        CancellationToken cancellationToken)
    {
        await requestContext.EnsureAdminAsync(cancellationToken);
        if (await userRepository.FindByIdAsync(request.Id, cancellationToken) is { IsBootstrap: true })
            throw new BusinessRuleException(ErrorCodes.BootstrapUserImmutable, "The bootstrap admin user cannot be disabled.");
        if (!await userRepository.DisableAsync(request.Id, cancellationToken))
            throw new NotFoundException(ErrorCodes.NotFound, "Security user was not found.");
        return new SecurityUserDisabledResponse(request.Id, false);
    }
}
