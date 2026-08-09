using System.Diagnostics;
using ShortenLink.Core.Domain;
using ShortenLink.Core.Generation;
using ShortenLink.Core.Services;
using ShortenLink.Core;
using ShortenLink.Core.Abstractions;
using ShortenLink.Core.Exceptions;
using ShortenLink.Core.Events;
using ShortenLink.Core.Diagnostics;
using ShortLinkDetailsResponse = ShortenLink.Core.Contracts.Responses.ShortLinkDetailsResponse;

namespace ShortenLink.Application.Services;

public sealed partial class ShortLinkService : IShortLinkService, ITenantAwareShortLinkService
{
    public Task<IReadOnlyList<ShortLink>> ListRecentAsync(
        int limit = 100,
        DateTimeOffset? beforeCreatedAt = null,
        string? beforeCode = null,
        CancellationToken cancellationToken = default) =>
        repository.ListRecentAsync(Math.Clamp(limit, 1, 500), beforeCreatedAt, beforeCode, cancellationToken);

    public Task<IReadOnlyList<ShortLink>> ListRecentPageAsync(
        int skip,
        int limit = 100,
        CancellationToken cancellationToken = default) =>
        repository.ListRecentPageAsync(Math.Max(skip, 0), Math.Clamp(limit, 1, 500), cancellationToken);

    public Task<IReadOnlyList<ShortLink>> ListAccessibleRecentAsync(
        int limit,
        DateTimeOffset? beforeCreatedAt,
        string? beforeCode,
        ShortLinkAccessScope accessScope,
        CancellationToken cancellationToken = default)
    {
        accessScope = NormalizeTenantScope(accessScope);
        return repository.ListAccessibleRecentAsync(
            Math.Clamp(limit, 1, 500),
            beforeCreatedAt,
            beforeCode,
            accessScope,
            cancellationToken);
    }

    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        repository.CountAsync(cancellationToken);

    public Task<ShortLinkListPage> ListPageAsync(
        int skip,
        int limit,
        string? filterExpression,
        ShortLinkListSortBy sortBy,
        ShortLinkSortDirection sortDirection,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var query = new ShortLinkListQuery(
            NormalizeFilter(filterExpression),
            sortBy,
            sortDirection,
            now);

        return repository.ListPageAsync(
            Math.Max(skip, 0),
            Math.Clamp(limit, 1, 500),
            query,
            cancellationToken);
    }

    public Task<ShortLinkListPage> ListAccessiblePageAsync(
        int skip,
        int limit,
        string? filterExpression,
        ShortLinkListSortBy sortBy,
        ShortLinkSortDirection sortDirection,
        ShortLinkAccessScope accessScope,
        CancellationToken cancellationToken = default,
        string? folder = null,
        string? tag = null)
    {
        accessScope = NormalizeTenantScope(accessScope);
        var now = timeProvider.GetUtcNow();
        var query = new ShortLinkListQuery(
            NormalizeFilter(filterExpression),
            sortBy,
            sortDirection,
            now,
            accessScope,
            Folder: folder,
            Tag: tag);
        return repository.ListPageAsync(
            Math.Max(skip, 0),
            Math.Clamp(limit, 1, 500),
            query,
            cancellationToken);
    }

    public Task<ShortLinkListPage> ListAccessibleCursorPageAsync(
        int limit,
        string? filterExpression,
        ShortLinkListSortBy sortBy,
        ShortLinkSortDirection sortDirection,
        DateTimeOffset beforeCreatedAt,
        string? beforeCode,
        ShortLinkAccessScope accessScope,
        CancellationToken cancellationToken = default,
        string? folder = null,
        string? tag = null)
    {
        accessScope = NormalizeTenantScope(accessScope);
        var now = timeProvider.GetUtcNow();
        var query = new ShortLinkListQuery(
            NormalizeFilter(filterExpression),
            sortBy,
            sortDirection,
            now,
            accessScope,
            beforeCreatedAt,
            string.IsNullOrWhiteSpace(beforeCode) ? null : beforeCode.Trim(),
            folder,
            tag);
        return repository.ListPageAsync(
            0,
            Math.Clamp(limit, 1, 501),
            query,
            cancellationToken);
    }

    private static string? NormalizeFilter(string? filterExpression) =>
        string.IsNullOrWhiteSpace(filterExpression) ? null : filterExpression.Trim();
}
