using ShortenLink.Core.Security;
using ShortenLink.Mediator;
using System.Text.RegularExpressions;

namespace ShortenLink.Application.Features.ShortLinks.List;

public sealed record ListShortLinksQuery(
    string BaseUrl,
    int? Limit,
    int? Page,
    string? Cursor,
    string? Search,
    string? Status,
    string? SortBy,
    string? SortDirection) : IRequest<ShortLinkAdminListResponse>;

public sealed record ShortLinkListQueryParameters(
    string? Search,
    string Status,
    string SortBy,
    string SortDirection);

public static partial class ShortLinkListQueryParameterParser
{
    public static ShortLinkListQueryParameters Parse(string? filter, string? sort)
    {
        var search = SearchPattern().Match(filter ?? string.Empty).Groups[1].Value;
        var status = StatusPattern().Match(filter ?? string.Empty).Groups[1].Value.ToLowerInvariant() switch
        {
            "true" => "active",
            "false" => "inactive",
            _ => "all"
        };
        var sortMatch = SortPattern().Match(sort ?? string.Empty);
        var field = sortMatch.Groups[2].Value.ToLowerInvariant() switch
        {
            "expiresat" => "expiry",
            "originalurl" => "destination",
            "code" => "code",
            "isactive" => "status",
            _ => "created"
        };

        return new ShortLinkListQueryParameters(
            string.IsNullOrWhiteSpace(search) ? null : search,
            status,
            field,
            sortMatch.Success && sortMatch.Groups[1].Value == "-" ? "desc" : "asc");
    }

    [GeneratedRegex(@"(?:Code|OriginalUrl) contains `([^`]*)`", RegexOptions.IgnoreCase)]
    private static partial Regex SearchPattern();

    [GeneratedRegex(@"IsActive eq `(true|false)`", RegexOptions.IgnoreCase)]
    private static partial Regex StatusPattern();

    [GeneratedRegex(@"^([+-]?)([A-Za-z][A-Za-z0-9.]*)$")]
    private static partial Regex SortPattern();
}

internal sealed class ListShortLinksQueryHandler(
    IShortLinkService shortLinkService,
    ShortLinkAccessGuard accessGuard)
    : IRequestHandler<ListShortLinksQuery, ShortLinkAdminListResponse>
{
    public async Task<ShortLinkAdminListResponse> Handle(
        ListShortLinksQuery request,
        CancellationToken cancellationToken)
    {
        var user = await accessGuard.GetAuthorizedUserAsync(
            ShortenLinkPermissionCatalog.ShortLinksRead, cancellationToken);
        var scope = await accessGuard.CreateScopeAsync(user, cancellationToken);
        var limit = Math.Clamp(request.Limit ?? 100, 1, 500);
        var hasListQuery = request.Page is not null
            || !string.IsNullOrWhiteSpace(request.Search)
            || !string.IsNullOrWhiteSpace(request.Status)
            || !string.IsNullOrWhiteSpace(request.SortBy)
            || !string.IsNullOrWhiteSpace(request.SortDirection);

        var status = ParseStatus(request.Status);
        var sortBy = ParseSortBy(request.SortBy);
        var direction = ParseDirection(request.SortDirection);
        if (request.Page is null
            && !string.IsNullOrWhiteSpace(request.Cursor)
            && status == ShortLinkListStatus.All
            && sortBy == ShortLinkListSortBy.Created
            && direction == ShortLinkSortDirection.Desc)
        {
            if (!ShortLinkFeatureSupport.TryDecodeCursor(
                request.Cursor, out var cursorCreatedAt, out var cursorCode))
            {
                throw new RequestValidationException(ErrorCodes.InvalidCursor, "Cursor is invalid.");
            }

            var cursorPage = await shortLinkService.ListAccessibleCursorPageAsync(
                Math.Min(limit + 1, 501),
                request.Search,
                status,
                sortBy,
                direction,
                cursorCreatedAt!.Value,
                cursorCode,
                scope,
                cancellationToken);
            var cursorItems = cursorPage.Items.Take(limit).ToList();
            var filteredNextCursor = cursorPage.Items.Count > limit && cursorItems.Count > 0
                ? ShortLinkFeatureSupport.EncodeCursor(
                    cursorItems[^1].CreatedAt,
                    cursorItems[^1].Code)
                : null;
            return new ShortLinkAdminListResponse(
                cursorItems.Select(link => Map(link, request.BaseUrl, scope)).ToList(),
                filteredNextCursor);
        }

        if (hasListQuery)
        {
            var page = Math.Max(request.Page ?? 1, 1);
            var result = await shortLinkService.ListAccessiblePageAsync(
                (page - 1) * limit,
                limit,
                request.Search,
                status,
                sortBy,
                direction,
                scope,
                cancellationToken);
            return new ShortLinkAdminListResponse(
                result.Items.Select(link => Map(link, request.BaseUrl, scope)).ToList(),
                null,
                result.TotalCount,
                page,
                limit,
                Math.Max(1, (int)Math.Ceiling(result.TotalCount / (double)limit)));
        }

        if (!ShortLinkFeatureSupport.TryDecodeCursor(
                request.Cursor, out var beforeCreatedAt, out var beforeCode))
            throw new RequestValidationException(ErrorCodes.InvalidCursor, "Cursor is invalid.");
        var links = await shortLinkService.ListAccessibleRecentAsync(
            limit + 1, beforeCreatedAt, beforeCode, scope, cancellationToken);
        var items = links.Take(limit).ToList();
        var nextCursor = links.Count > limit
            ? ShortLinkFeatureSupport.EncodeCursor(items[^1].CreatedAt, items[^1].Code)
            : null;
        return new ShortLinkAdminListResponse(
            items.Select(link => Map(link, request.BaseUrl, scope)).ToList(),
            nextCursor);
    }

    private static ShortLinkAdminListItemResponse Map(
        ShortLink link,
        string baseUrl,
        ShortLinkAccessScope scope) =>
        ShortLinkAdminListItemResponse.FromDomain(
            link,
            ShortLinkFeatureSupport.BuildShortUrl(baseUrl, link.Code),
            ShortLinkAccessGuard.GetAccessLevel(link, scope));

    private static ShortLinkListStatus ParseStatus(string? value) =>
        value?.ToLowerInvariant() switch
        {
            null or "" or "all" => ShortLinkListStatus.All,
            "active" => ShortLinkListStatus.Active,
            "inactive" => ShortLinkListStatus.Inactive,
            "expired" => ShortLinkListStatus.Expired,
            "expiring-soon" => ShortLinkListStatus.ExpiringSoon,
            _ => throw new RequestValidationException(ErrorCodes.InvalidFilter, "Status filter is invalid.")
        };

    private static ShortLinkListSortBy ParseSortBy(string? value) =>
        value?.ToLowerInvariant() switch
        {
            null or "" or "created" => ShortLinkListSortBy.Created,
            "expiry" => ShortLinkListSortBy.Expiry,
            "destination" => ShortLinkListSortBy.Destination,
            "code" => ShortLinkListSortBy.Code,
            "status" => ShortLinkListSortBy.Status,
            _ => throw new RequestValidationException(ErrorCodes.InvalidSort, "Sort field is invalid.")
        };

    private static ShortLinkSortDirection ParseDirection(string? value) =>
        value?.ToLowerInvariant() switch
        {
            null or "" or "desc" => ShortLinkSortDirection.Desc,
            "asc" => ShortLinkSortDirection.Asc,
            _ => throw new RequestValidationException(ErrorCodes.InvalidSortDirection, "Sort direction is invalid.")
        };
}
