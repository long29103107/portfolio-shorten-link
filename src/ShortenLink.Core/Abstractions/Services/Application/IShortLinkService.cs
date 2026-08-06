using ShortenLink.Core.Services;

namespace ShortenLink.Core.Abstractions;

public interface IShortLinkService
{
    Task<IReadOnlyList<Domain.ShortLinkEntity>> ListRecentAsync(
        int limit = 100,
        DateTimeOffset? beforeCreatedAt = null,
        string? beforeCode = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Domain.ShortLinkEntity>> ListRecentPageAsync(
        int skip,
        int limit = 100,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Domain.ShortLinkEntity>> ListAccessibleRecentAsync(
        int limit,
        DateTimeOffset? beforeCreatedAt,
        string? beforeCode,
        ShortLinkAccessScope accessScope,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);

    Task<ShortLinkListPage> ListPageAsync(
        int skip,
        int limit,
        string? filterExpression,
        ShortLinkListSortBy sortBy,
        ShortLinkSortDirection sortDirection,
        CancellationToken cancellationToken = default);

    Task<ShortLinkListPage> ListAccessiblePageAsync(
        int skip,
        int limit,
        string? filterExpression,
        ShortLinkListSortBy sortBy,
        ShortLinkSortDirection sortDirection,
        ShortLinkAccessScope accessScope,
        CancellationToken cancellationToken = default);

    Task<ShortLinkListPage> ListAccessibleCursorPageAsync(
        int limit,
        string? filterExpression,
        ShortLinkListSortBy sortBy,
        ShortLinkSortDirection sortDirection,
        DateTimeOffset beforeCreatedAt,
        string? beforeCode,
        ShortLinkAccessScope accessScope,
        CancellationToken cancellationToken = default) =>
        ListAccessiblePageAsync(
            0,
            limit,
            filterExpression,
            sortBy,
            sortDirection,
            accessScope,
            cancellationToken);

    Task<CreateShortLinkResponse> CreateAsync(
        CreateShortLinkRequest request,
        CancellationToken cancellationToken = default);

    Task<ResolveShortLinkResponse> ResolveAsync(
        string code,
        CancellationToken cancellationToken = default);

    Task<ShortLinkDetailsResponse> GetDetailsAsync(
        string code,
        CancellationToken cancellationToken = default);

    Task<ShortLinkDetailsResponse> UpdateAsync(
        string code,
        UpdateShortLinkRequest request,
        CancellationToken cancellationToken = default);

    Task<DeactivateShortLinkResponse> DeactivateAsync(
        string code,
        CancellationToken cancellationToken = default);

    Task<DeactivateShortLinkResponse> ActivateAsync(
        string code,
        CancellationToken cancellationToken = default);

    Task<DeactivateShortLinkResponse> DeleteAsync(
        string code,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Optional resolve capability for hosts that provide trusted tenant context.
/// </summary>
public interface ITenantAwareShortLinkService
{
    Task<ResolveShortLinkResponse> ResolveAsync(
        string code,
        CancellationToken cancellationToken,
        string tenantId);
}
