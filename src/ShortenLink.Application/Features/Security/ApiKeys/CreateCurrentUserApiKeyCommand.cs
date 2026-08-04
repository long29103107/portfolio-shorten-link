using ShortenLink.Application.Abstractions;
using ShortenLink.Application.Features.Audit;
using ShortenLink.Core.Abstractions;
using ShortenLink.Core.Security;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Features.Security.ApiKeys;

public sealed record CreateCurrentUserApiKeyCommand(
    string DisplayName) : IRequest<SecurityUserApiKeyCreatedResponse>;

internal sealed class CreateCurrentUserApiKeyCommandHandler(
    ICurrentRequestContext requestContext,
    IShortenLinkUserApiKeyRepository apiKeyRepository,
    TimeProvider timeProvider,
    ISecureTokenGenerator tokenGenerator,
    ShortLinkAuditWriter auditWriter)
    : IRequestHandler<CreateCurrentUserApiKeyCommand, SecurityUserApiKeyCreatedResponse>
{
    public async Task<SecurityUserApiKeyCreatedResponse> Handle(
        CreateCurrentUserApiKeyCommand request,
        CancellationToken cancellationToken)
    {
        var user = await requestContext.GetCurrentUserAsync(cancellationToken)
            ?? throw new AuthenticationRequiredException();
        var rawApiKey = $"slk_{tokenGenerator.CreateToken(32)}";
        var apiKey = new ShortenLinkUserApiKey(
            tokenGenerator.CreateIdentifier(),
            user.UserId,
            request.DisplayName.Trim(),
            ShortenLinkSecurityCredentialHasher.HashApiKey(rawApiKey),
            isEnabled: true,
            timeProvider.GetUtcNow());

        await apiKeyRepository.AddOrUpdateAsync(apiKey, cancellationToken);
        await auditWriter.RecordAsync(
            user.UserId,
            ShortLinkAuditActions.UserApiKeyCreated,
            apiKey.ApiKeyKey,
            user.UserId,
            subjectUserId: user.UserId,
            targetType: ShortLinkAuditTargetTypes.UserApiKey,
            cancellationToken: cancellationToken);

        return new SecurityUserApiKeyCreatedResponse(
            SecurityUserApiKeyResponse.FromDomain(apiKey),
            rawApiKey);
    }
}
