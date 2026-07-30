using ShortenLink.Application.Abstractions;
using ShortenLink.Application.Features.Audit;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Features.Security.Users;

public sealed record DisableSecurityUserCommand(
    string Id) : IRequest<SecurityUserDisabledResponse>;

internal sealed class DisableSecurityUserCommandHandler(
    ICurrentRequestContext requestContext,
    IShortenLinkSecurityUserRepository userRepository,
    ShortLinkAuditWriter auditWriter)
    : IRequestHandler<DisableSecurityUserCommand, SecurityUserDisabledResponse>
{
    public async Task<SecurityUserDisabledResponse> Handle(
        DisableSecurityUserCommand request,
        CancellationToken cancellationToken)
    {
        var actor = await requestContext.AuthorizeAsync(
            SecurityFeatureSupport.AdminOnly,
            cancellationToken);
        var existing = await userRepository.FindByIdAsync(request.Id, cancellationToken);
        if (existing is { IsBootstrap: true })
            throw new BusinessRuleException(ErrorCodes.BootstrapUserImmutable, "The bootstrap admin user cannot be disabled.");
        if (!await userRepository.DisableAsync(request.Id, cancellationToken))
            throw new NotFoundException(ErrorCodes.NotFound, "Security user was not found.");
        await auditWriter.RecordAsync(
            actor,
            ShortLinkAuditActions.SecurityUserDisabled,
            existing!.UserKey,
            ownerUserId: null,
            subjectUserId: existing.UserKey,
            cancellationToken: cancellationToken,
            targetType: ShortLinkAuditTargetTypes.SecurityUser);
        return new SecurityUserDisabledResponse(request.Id, false);
    }
}
