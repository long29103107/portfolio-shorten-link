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
        string? search,
        ShortLinkListStatus status,
        ShortLinkListSortBy sortBy,
        ShortLinkSortDirection sortDirection,
        CancellationToken cancellationToken = default);

    Task<ShortLinkListPage> ListAccessiblePageAsync(
        int skip,
        int limit,
        string? search,
        ShortLinkListStatus status,
        ShortLinkListSortBy sortBy,
        ShortLinkSortDirection sortDirection,
        ShortLinkAccessScope accessScope,
        CancellationToken cancellationToken = default);

    Task<CreateShortLinkResult> CreateAsync(
        CreateShortLinkRequest request,
        CancellationToken cancellationToken = default);

    Task<ResolveShortLinkResult> ResolveAsync(
        string code,
        CancellationToken cancellationToken = default);

    Task<ShortLinkDetailsResult> GetDetailsAsync(
        string code,
        CancellationToken cancellationToken = default);

    Task<ShortLinkDetailsResult> UpdateAsync(
        string code,
        UpdateShortLinkRequest request,
        CancellationToken cancellationToken = default);

    Task<DeactivateShortLinkResult> DeactivateAsync(
        string code,
        CancellationToken cancellationToken = default);

    Task<DeactivateShortLinkResult> ActivateAsync(
        string code,
        CancellationToken cancellationToken = default);

    Task<DeactivateShortLinkResult> DeleteAsync(
        string code,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Optional resolve capability for hosts that provide trusted tenant context.
/// </summary>
public interface ITenantAwareShortLinkService
{
    Task<ResolveShortLinkResult> ResolveAsync(
        string code,
        CancellationToken cancellationToken,
        string tenantId);
}
