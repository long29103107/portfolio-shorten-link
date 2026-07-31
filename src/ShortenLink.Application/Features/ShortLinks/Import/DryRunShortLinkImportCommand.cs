using ShortenLink.Application.Abstractions;
using ShortenLink.Core.Contracts.Requests;
using ShortenLink.Core.Contracts.Results;
using ShortenLink.Core.Security;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Features.ShortLinks.Import;

public sealed record DryRunShortLinkImportCommand(
    IReadOnlyList<ShortLinkImportItemRequest>? Items) : IRequest<ShortLinkImportDryRunResult>;

internal sealed class DryRunShortLinkImportCommandHandler(
    IShortLinkImportValidator validator,
    ICurrentRequestContext requestContext)
    : IRequestHandler<DryRunShortLinkImportCommand, ShortLinkImportDryRunResult>
{
    public async Task<ShortLinkImportDryRunResult> Handle(
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
