using ShortenLink.Core.Domain;
using ShortenLink.Core.Security;

namespace ShortenLink.Core.Contracts.Queries;

public sealed record ShortLinkListQuery(
    string? Search,
    ShortLinkListStatus Status,
    ShortLinkListSortBy SortBy,
    ShortLinkSortDirection SortDirection,
    DateTimeOffset Now,
    DateTimeOffset ExpiringSoonBefore,
    ShortLinkAccessScope? AccessScope = null,
    DateTimeOffset? BeforeCreatedAt = null,
    string? BeforeCode = null);

public sealed record ShortLinkAccessScope(
    string? UserId,
    bool IsAdmin,
    IReadOnlyDictionary<string, ShortLinkShareAccess> SharedAccess,
    string? TenantId = null);

public enum ShortLinkListStatus
{
    All,
    Active,
    Inactive,
    Expired,
    ExpiringSoon
}

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
