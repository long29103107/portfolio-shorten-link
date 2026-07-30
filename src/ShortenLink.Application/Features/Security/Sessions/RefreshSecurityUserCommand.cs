using ShortenLink.Application.Abstractions;
using ShortenLink.Application.Features.Audit;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Features.Security.Sessions;

public sealed record RefreshSecurityUserCommand(
    string RefreshToken) : IRequest<SecurityLoginResponse>;

internal sealed class RefreshSecurityUserCommandHandler(
    ISecuritySessionService sessionService,
    TimeProvider timeProvider,
    ShortLinkAuditWriter auditWriter)
    : IRequestHandler<RefreshSecurityUserCommand, SecurityLoginResponse>
{
    public async Task<SecurityLoginResponse> Handle(
        RefreshSecurityUserCommand request,
        CancellationToken cancellationToken)
    {
        var result = await sessionService.RefreshAsync(request.RefreshToken, cancellationToken);
        var response = LoginSecurityUserCommandHandler.CreateResponse(
            result,
            timeProvider.GetUtcNow());
        await auditWriter.RecordAsync(
            response.User.UserId,
            ShortLinkAuditActions.AuthenticationRefresh,
            response.User.UserId,
            response.User.UserId,
            subjectUserId: response.User.UserId,
            targetType: ShortLinkAuditTargetTypes.Authentication,
            cancellationToken: cancellationToken);
        return response;
    }
}
