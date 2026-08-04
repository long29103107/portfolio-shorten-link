using ShortenLink.Application.Abstractions;
using ShortenLink.Core.Contracts.Requests;
using ShortenLink.Core.Contracts.Responses;
using ShortenLink.Core.Security;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Features.ShortLinks.Import;

public sealed record DryRunShortLinkImportCommand(
    IReadOnlyList<ShortLinkImportItemRequest>? Items) : IRequest<ShortLinkImportDryRunResponse>;

internal sealed class DryRunShortLinkImportCommandHandler(
    IShortLinkImportValidator validator,
    ICurrentRequestContext requestContext)
    : IRequestHandler<DryRunShortLinkImportCommand, ShortLinkImportDryRunResponse>
{
    public async Task<ShortLinkImportDryRunResponse> Handle(
        DryRunShortLinkImportCommand request,
        CancellationToken cancellationToken)
    {
        await requestContext.AuthorizeAsync(
            ShortenLinkPermissionCatalog.ShortLinksImport,
            cancellationToken);

        return await validator.ValidateDryRunAsync(
            ToAsyncEnumerable(request.Items ?? []),
            cancellationToken);
    }

    private static async IAsyncEnumerable<ShortLinkImportItemRequest> ToAsyncEnumerable(
        IReadOnlyList<ShortLinkImportItemRequest> items)
    {
        foreach (var item in items)
        {
            yield return item;
            await Task.Yield();
        }
    }
}
