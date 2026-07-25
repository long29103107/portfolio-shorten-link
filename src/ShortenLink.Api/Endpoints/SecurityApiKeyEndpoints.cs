using ShortenLink.Application.Features.Security.ApiKeys;
using ShortenLink.Mediator;

namespace ShortenLink.Api.Endpoints;

internal static class SecurityApiKeyEndpoints
{
    public static IEndpointRouteBuilder MapSecurityApiKeyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/security/api-keys")
            .WithTags("Security API Keys");

        group.MapGet(
                "/",
                static (ISender sender, CancellationToken cancellationToken) =>
                    sender.Send(new ListCurrentUserApiKeysQuery(), cancellationToken))
            .WithName("ListCurrentUserApiKeys");
        group.MapPost(
                "/",
                static (
                    SecurityUserApiKeyCreateRequest request,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                    sender.Send(
                        new CreateCurrentUserApiKeyCommand(request.DisplayName),
                        cancellationToken))
            .WithName("CreateCurrentUserApiKey");
        group.MapPut(
                "/{id}",
                static (
                    string id,
                    SecurityUserApiKeyRenameRequest request,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                    sender.Send(
                        new RenameCurrentUserApiKeyCommand(id, request.DisplayName),
                        cancellationToken))
            .WithName("RenameCurrentUserApiKey");
        group.MapPost(
                "/{id}/disable",
                static (string id, ISender sender, CancellationToken cancellationToken) =>
                    sender.Send(new DisableCurrentUserApiKeyCommand(id), cancellationToken))
            .WithName("DisableCurrentUserApiKey");

        return endpoints;
    }
}
