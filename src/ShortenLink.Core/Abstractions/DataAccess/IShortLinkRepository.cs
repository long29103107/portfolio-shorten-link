using ShortenLink.Core.Domain;
using ShortenLink.Core.Services;

namespace ShortenLink.Core.Abstractions;

public interface IShortLinkRepository
{
    Task<IReadOnlyList<ShortLink>> ListRecentAsync(
        int limit,
        DateTimeOffset? beforeCreatedAt = null,
        string? beforeCode = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShortLink>> ListAccessibleRecentAsync(
        int limit,
        DateTimeOffset? beforeCreatedAt,
        string? beforeCode,
        ShortLinkAccessScope accessScope,
        CancellationToken cancellationToken = default) =>
        ListRecentAsync(limit, beforeCreatedAt, beforeCode, cancellationToken);

    Task<IReadOnlyList<ShortLink>> ListRecentPageAsync(
        int skip,
        int limit,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);

    Task<ShortLinkListPage> ListPageAsync(
        int skip,
        int limit,
        ShortLinkListQuery query,
        CancellationToken cancellationToken = default);

    Task<ShortLink?> FindByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task AddAsync(ShortLink shortLink, CancellationToken cancellationToken = default);

    Task UpdateAsync(ShortLink shortLink, CancellationToken cancellationToken = default);

    Task DeleteAsync(string code, CancellationToken cancellationToken = default);
}

/// <summary>
/// Optional provider contract for durable create-request idempotency.
/// Providers implementing this contract must enforce a unique key at their
/// persistence boundary and throw <see cref="ShortLinkIdempotencyConflictException"/>
/// when concurrent writers race on the same key.
/// </summary>
public interface IShortLinkIdempotencyRepository
{
    Task<ShortLink?> FindByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Optional provider capability for tenant-partitioned short links. Providers
/// implementing this contract must persist <see cref="ShortLink.TenantId"/>,
/// enforce tenant filters whenever an access scope is supplied, and scope
/// idempotency uniqueness to the normalized tenant identifier.
/// </summary>
public interface IShortLinkTenantRepository
{
    Task<ShortLink?> FindByTenantIdempotencyKeyAsync(
        string tenantId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}
