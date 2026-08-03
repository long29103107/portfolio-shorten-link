using ShortenLink.Core.Domain;

namespace ShortenLink.Core.Abstractions;

/// <summary>
/// Optional read-only provider capability for deterministic expiration batches.
/// Implementations must scope tenantId to the normalized partition and apply the
/// cursor before returning candidates.
/// </summary>
public interface IShortLinkExpirationRepository
{
    Task<IReadOnlyList<ShortLink>> ListExpirationCandidatesAsync(
        string? tenantId,
        DateTimeOffset? beforeExpiresAt,
        string? beforeCode,
        int limit,
        CancellationToken cancellationToken = default);
}
