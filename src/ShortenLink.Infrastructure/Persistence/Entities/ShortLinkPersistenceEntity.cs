namespace ShortenLink.Infrastructure.Persistence.Entities;

public sealed class ShortLinkPersistenceEntity : BaseEntity<Guid>
{
    public string Code { get; set; } = string.Empty;

    public string OriginalUrl { get; set; } = string.Empty;

    public DateTimeOffset? ExpiresAt { get; set; }

    public bool IsActive { get; set; }

    public string? CreatedByUserId { get; set; }

    public string? CreatedByDisplayName { get; set; }

    public string? CreatedByUsername { get; set; }

    public string? IdempotencyKey { get; set; }

    public static ShortLinkPersistenceEntity FromDomain(ShortLink shortLink)
    {
        ArgumentNullException.ThrowIfNull(shortLink);

        return new ShortLinkPersistenceEntity
        {
            Id = shortLink.Id,
            Code = shortLink.Code,
            OriginalUrl = shortLink.OriginalUrl.AbsoluteUri,
            CreatedAt = shortLink.CreatedAt,
            ExpiresAt = shortLink.ExpiresAt,
            IsActive = shortLink.IsActive,
            CreatedByUserId = shortLink.CreatedByUserId,
            CreatedByDisplayName = shortLink.CreatedByDisplayName,
            CreatedByUsername = shortLink.CreatedByUsername,
            IdempotencyKey = shortLink.IdempotencyKey
        };
    }

    public ShortLink ToDomain() =>
        new(
            Code,
            new Uri(OriginalUrl),
            CreatedAt,
            ExpiresAt,
            IsActive,
            CreatedByUserId,
            CreatedByDisplayName,
            CreatedByUsername,
            Id,
            IdempotencyKey);

    public void UpdateFromDomain(ShortLink shortLink)
    {
        ArgumentNullException.ThrowIfNull(shortLink);

        OriginalUrl = shortLink.OriginalUrl.AbsoluteUri;
        CreatedAt = shortLink.CreatedAt;
        ExpiresAt = shortLink.ExpiresAt;
        IsActive = shortLink.IsActive;
        CreatedByUserId = shortLink.CreatedByUserId;
        CreatedByDisplayName = shortLink.CreatedByDisplayName;
        CreatedByUsername = shortLink.CreatedByUsername;
        IdempotencyKey = shortLink.IdempotencyKey;
    }
}
