using ShortenLink.Core.Security;
using ShortenLink.Core.Querying;
using ShortenLink.Core.Contracts.Requests;
using ShortenLink.Mediator;
using System.Text.RegularExpressions;

namespace ShortenLink.Application.Features.ShortLinks.List;

public sealed record ListShortLinksQuery(
    string BaseUrl,
    ShortLinkListEndpointRequest Params) : IRequest<ShortLinkAdminListResponse>;

public sealed record ShortLinkSortParameters(
    string SortBy,
    string SortDirection);

public static partial class ShortLinkListQueryParameterParser
{
    public static ShortLinkSortParameters ParseSort(string? sort)
    {
        var sortMatch = SortPattern().Match(sort ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(sort) && !sortMatch.Success)
        {
            throw new RequestValidationException(ErrorCodes.InvalidSort, "Sort expression is invalid.");
        }

        var field = sortMatch.Groups[2].Value.ToLowerInvariant() switch
        {
            "" => "created",
            "createdat" => "created",
            "expiresat" => "expiry",
            "originalurl" => "destination",
            "code" => "code",
            "isactive" => "status",
            _ => throw new RequestValidationException(ErrorCodes.InvalidSort, "Sort field is invalid.")
        };

        return new ShortLinkSortParameters(
            field,
            sortMatch.Success && sortMatch.Groups[1].Value == "-" ? "desc" : "asc");
    }

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
        var parameters = request.Params;
        ValidateFilter(parameters.Fe);
        var user = await accessGuard.GetAuthorizedUserAsync(
            ShortenLinkPermissionCatalog.ShortLinksRead, cancellationToken);
        var scope = await accessGuard.CreateScopeAsync(user, cancellationToken);
        var limit = Math.Clamp(parameters.Limit ?? 100, 1, 500);
        var hasListQuery = parameters.Page is not null
            || !string.IsNullOrWhiteSpace(parameters.Fe)
            || !string.IsNullOrWhiteSpace(parameters.Sort);

        var parsedSort = ShortLinkListQueryParameterParser.ParseSort(parameters.Sort);
        var sortBy = ParseSortBy(parsedSort.SortBy);
        var direction = ParseDirection(parsedSort.SortDirection);
        if (parameters.Page is null
            && !string.IsNullOrWhiteSpace(parameters.Cursor)
            && sortBy == ShortLinkListSortBy.Created
            && direction == ShortLinkSortDirection.Desc)
        {
            if (!ShortLinkFeatureSupport.TryDecodeCursor(
                parameters.Cursor, out var cursorCreatedAt, out var cursorCode))
            {
                throw new RequestValidationException(ErrorCodes.InvalidCursor, "Cursor is invalid.");
            }

            var cursorPage = await shortLinkService.ListAccessibleCursorPageAsync(
                Math.Min(limit + 1, 501),
                parameters.Fe,
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
            var page = Math.Max(parameters.Page ?? 1, 1);
            var result = await shortLinkService.ListAccessiblePageAsync(
                (page - 1) * limit,
                limit,
                parameters.Fe,
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
                parameters.Cursor, out var beforeCreatedAt, out var beforeCode))
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

    private static void ValidateFilter(string? filterExpression)
    {
        if (string.IsNullOrWhiteSpace(filterExpression))
        {
            return;
        }

        try
        {
            _ = FilterExpressionParser.Parse<ShortLinkFilterFields>(
                filterExpression,
                ShortLinkFilterFields.AllowedProperties);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
        {
            throw new RequestValidationException(
                ErrorCodes.InvalidFilter,
                exception.Message);
        }
    }

    private sealed class ShortLinkFilterFields
    {
        public static readonly string[] AllowedProperties =
        [
            nameof(Code),
            nameof(OriginalUrl),
            nameof(ExpiresAt),
            nameof(ActiveFrom),
            nameof(IsActive),
            nameof(CreatedAt),
            nameof(CreatedByUserId)
        ];

        public string Code { get; init; } = string.Empty;
        public string OriginalUrl { get; init; } = string.Empty;
        public DateTimeOffset? ExpiresAt { get; init; }
        public DateTimeOffset? ActiveFrom { get; init; }
        public bool IsActive { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public string? CreatedByUserId { get; init; }
    }
}
