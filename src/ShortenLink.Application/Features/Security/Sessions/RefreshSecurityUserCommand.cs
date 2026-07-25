using ShortenLink.Application.Abstractions;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Features.Security.Sessions;

public sealed record RefreshSecurityUserCommand(
    string RefreshToken) : IRequest<SecurityLoginResponse>;

internal sealed class RefreshSecurityUserCommandHandler(
    ISecuritySessionService sessionService)
    : IRequestHandler<RefreshSecurityUserCommand, SecurityLoginResponse>
{
    public async Task<SecurityLoginResponse> Handle(
        RefreshSecurityUserCommand request,
        CancellationToken cancellationToken)
    {
        var result = await sessionService.RefreshAsync(request.RefreshToken, cancellationToken);
        return LoginSecurityUserCommandHandler.CreateResponse(result);
    }
}
