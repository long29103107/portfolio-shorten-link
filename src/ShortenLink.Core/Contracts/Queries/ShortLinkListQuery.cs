using ShortenLink.Core.Domain;
using ShortenLink.Core.Security;

namespace ShortenLink.Core.Contracts.Queries;

public sealed record ShortLinkListQuery(
    string? FilterExpression,
    ShortLinkListSortBy SortBy,
    ShortLinkSortDirection SortDirection,
    DateTimeOffset Now,
    ShortLinkAccessScope? AccessScope = null,
    DateTimeOffset? BeforeCreatedAt = null,
    string? BeforeCode = null);

public sealed record ShortLinkAccessScope(
    string? UserId,
    bool IsAdmin,
    IReadOnlyDictionary<string, ShortLinkShareAccess> SharedAccess,
    string? TenantId = null);

public enum ShortLinkListSortBy
{
    Created,
    Expiry,
    Destination,
    Code,
    Status
}

public enum ShortLinkSortDirection
{
    Asc,
    Desc
}
