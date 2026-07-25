using ShortenLink.Application.Abstractions;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Features.Security.ApiKeys;

public sealed record RenameCurrentUserApiKeyCommand(
    string Id,
    string DisplayName) : IRequest<SecurityUserApiKeyResponse>;

internal sealed class RenameCurrentUserApiKeyCommandHandler(
    ICurrentRequestContext requestContext,
    IShortenLinkUserApiKeyRepository apiKeyRepository)
    : IRequestHandler<RenameCurrentUserApiKeyCommand, SecurityUserApiKeyResponse>
{
    public async Task<SecurityUserApiKeyResponse> Handle(
        RenameCurrentUserApiKeyCommand request,
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
        var apiKey = await apiKeyRepository.FindByIdAsync(request.Id, cancellationToken);
        if (apiKey is null || !apiKey.UserId.Equals(user.UserId, StringComparison.Ordinal))
        {
            throw new NotFoundException(ErrorCodes.NotFound, "API key was not found.");
        }

        var renamed = new ShortenLinkUserApiKey(
            apiKey.ApiKeyKey,
            apiKey.UserId,
            request.DisplayName.Trim(),
            apiKey.KeyHash,
            apiKey.IsEnabled,
            apiKey.CreatedAt);

        await apiKeyRepository.AddOrUpdateAsync(renamed, cancellationToken);
        return SecurityUserApiKeyResponse.FromDomain(renamed);
    }
}
