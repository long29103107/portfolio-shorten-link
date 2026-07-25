using System.Globalization;
using System.Security.Cryptography;
using ShortenLink.Application.Abstractions;
using ShortenLink.Core.Security;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Features.Security.ApiKeys;

public sealed record CreateCurrentUserApiKeyCommand(
    string DisplayName) : IRequest<SecurityUserApiKeyCreatedResponse>;

internal sealed class CreateCurrentUserApiKeyCommandHandler(
    ICurrentRequestContext requestContext,
    IShortenLinkUserApiKeyRepository apiKeyRepository,
    TimeProvider timeProvider)
    : IRequestHandler<CreateCurrentUserApiKeyCommand, SecurityUserApiKeyCreatedResponse>
{
    public async Task<SecurityUserApiKeyCreatedResponse> Handle(
        CreateCurrentUserApiKeyCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            throw new RequestValidationException(
                ErrorCodes.InvalidApiKey,
                "API key display name is required.",
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
                {
                    ["displayName"] = ["API key display name is required."]
                });
        }

        var user = await requestContext.GetCurrentUserAsync(cancellationToken)
            ?? throw new AuthenticationRequiredException();
        var rawApiKey = $"slk_{Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant()}";
        var apiKey = new ShortenLinkUserApiKey(
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture),
            user.UserId,
            request.DisplayName.Trim(),
            ShortenLinkSecurityCredentialHasher.HashApiKey(rawApiKey),
            isEnabled: true,
            timeProvider.GetUtcNow());

        await apiKeyRepository.AddOrUpdateAsync(apiKey, cancellationToken);

        return new SecurityUserApiKeyCreatedResponse(
            SecurityUserApiKeyResponse.FromDomain(apiKey),
            rawApiKey);
    }
}
