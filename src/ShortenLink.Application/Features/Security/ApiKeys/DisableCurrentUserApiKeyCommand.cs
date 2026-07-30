using ShortenLink.Application.Abstractions;
using ShortenLink.Application.Features.Audit;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Features.Security.ApiKeys;

public sealed record DisableCurrentUserApiKeyCommand(
    string Id) : IRequest<SecurityUserApiKeyDisabledResponse>;

internal sealed class DisableCurrentUserApiKeyCommandHandler(
    ICurrentRequestContext requestContext,
    IShortenLinkUserApiKeyRepository apiKeyRepository,
    ShortLinkAuditWriter auditWriter)
    : IRequestHandler<DisableCurrentUserApiKeyCommand, SecurityUserApiKeyDisabledResponse>
{
    public async Task<SecurityUserApiKeyDisabledResponse> Handle(
        DisableCurrentUserApiKeyCommand request,
        CancellationToken cancellationToken)
    {
        var user = await requestContext.GetCurrentUserAsync(cancellationToken)
            ?? throw new AuthenticationRequiredException();
        var apiKey = await apiKeyRepository.FindByIdAsync(request.Id, cancellationToken);
        if (apiKey is null || !apiKey.UserId.Equals(user.UserId, StringComparison.Ordinal))
        {
            throw new NotFoundException(ErrorCodes.NotFound, "API key was not found.");
        }

        if (!await apiKeyRepository.DisableAsync(request.Id, cancellationToken))
        {
            throw new NotFoundException(ErrorCodes.NotFound, "API key was not found.");
        }

        await auditWriter.RecordAsync(
            user.UserId,
            ShortLinkAuditActions.UserApiKeyDisabled,
            apiKey.ApiKeyKey,
            user.UserId,
            subjectUserId: user.UserId,
            targetType: ShortLinkAuditTargetTypes.UserApiKey,
            cancellationToken: cancellationToken);
        return new SecurityUserApiKeyDisabledResponse(request.Id, false);
    }
}
