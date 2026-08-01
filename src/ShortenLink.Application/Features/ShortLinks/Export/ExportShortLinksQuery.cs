using System.Runtime.CompilerServices;
using ShortenLink.Application.Contracts.Responses;
using ShortenLink.Core.Abstractions;
using ShortenLink.Core.Security;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Features.ShortLinks.Export;

public sealed record ExportShortLinksQuery(int? Limit)
    : IRequest<IAsyncEnumerable<ShortLinkExportRecord>>;

public static class ShortLinkExportLimits
{
    public const int DefaultItems = 100;
    public const int MaxItems = 1_000;
    internal const int PageSize = 100;

    public static int Clamp(int? limit) =>
        Math.Clamp(limit ?? DefaultItems, 1, MaxItems);
}

internal sealed class ExportShortLinksQueryHandler(
    IShortLinkService shortLinkService,
    ShortLinkAccessGuard accessGuard)
    : IRequestHandler<ExportShortLinksQuery, IAsyncEnumerable<ShortLinkExportRecord>>
{
    public async Task<IAsyncEnumerable<ShortLinkExportRecord>> Handle(
        ExportShortLinksQuery request,
        CancellationToken cancellationToken)
    {
        var actor = await accessGuard.GetAuthorizedUserAsync(
            ShortenLinkPermissionCatalog.ShortLinksRead,
            cancellationToken);
        var scope = await accessGuard.CreateScopeAsync(actor, cancellationToken);

        return ReadAsync(
            shortLinkService,
            scope,
            ShortLinkExportLimits.Clamp(request.Limit),
            cancellationToken);
    }

    private static async IAsyncEnumerable<ShortLinkExportRecord> ReadAsync(
        IShortLinkService shortLinkService,
        ShortLinkAccessScope accessScope,
        int limit,
        CancellationToken requestCancellationToken,
        [EnumeratorCancellation] CancellationToken enumerationCancellationToken = default)
    {
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            requestCancellationToken,
            enumerationCancellationToken);
        var cancellationToken = linkedCancellation.Token;
        DateTimeOffset? beforeCreatedAt = null;
        string? beforeCode = null;
        var remaining = limit;

        while (remaining > 0)
        {
            var pageSize = Math.Min(ShortLinkExportLimits.PageSize, remaining);
            var page = await shortLinkService.ListAccessibleRecentAsync(
                pageSize,
                beforeCreatedAt,
                beforeCode,
                accessScope,
                cancellationToken);
            if (page.Count == 0)
            {
                yield break;
            }

            foreach (var shortLink in page)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return ShortLinkExportRecord.FromDomain(
                    shortLink,
                    ShortLinkAccessGuard.GetAccessLevel(shortLink, accessScope));
                remaining--;
                if (remaining == 0)
                {
                    yield break;
                }
            }

            if (page.Count < pageSize)
            {
                yield break;
            }

            var last = page[^1];
            beforeCreatedAt = last.CreatedAt;
            beforeCode = last.Code;
        }
    }
}
