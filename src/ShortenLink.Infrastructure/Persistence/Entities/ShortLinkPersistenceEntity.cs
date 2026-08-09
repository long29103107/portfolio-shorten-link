using ShortenLink.Core.Security;

namespace ShortenLink.Infrastructure.Persistence.Entities;

public sealed class ShortLinkPersistenceEntity : BaseEntity<Guid>
{
    public string Code { get; set; } = string.Empty;

    public string OriginalUrl { get; set; } = string.Empty;

    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset? ActiveFrom { get; set; }

    public int? MaxClicks { get; set; }

    public int ClickCount { get; set; }

    public bool IsActive { get; set; }

    public string? CreatedByUserId { get; set; }

    public string? CreatedByDisplayName { get; set; }

    public string? CreatedByUsername { get; set; }

    public string? IdempotencyKey { get; set; }

    public string TenantId { get; set; } = string.Empty;

    public ShortLinkSharingMode SharingMode { get; set; } = ShortLinkSharingMode.AllowList;

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
            ActiveFrom = shortLink.ActiveFrom,
            MaxClicks = shortLink.MaxClicks,
            ClickCount = shortLink.ClickCount,
            IsActive = shortLink.IsActive,
            CreatedByUserId = shortLink.CreatedByUserId,
            CreatedByDisplayName = shortLink.CreatedByDisplayName,
            CreatedByUsername = shortLink.CreatedByUsername,
            IdempotencyKey = shortLink.IdempotencyKey,
            TenantId = shortLink.TenantId ?? string.Empty,
            SharingMode = shortLink.SharingMode
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
            IdempotencyKey,
            TenantId,
            sharingMode: SharingMode,
            activeFrom: ActiveFrom,
            maxClicks: MaxClicks,
            clickCount: ClickCount);

    public void UpdateFromDomain(ShortLink shortLink)
    {
        ArgumentNullException.ThrowIfNull(shortLink);

        OriginalUrl = shortLink.OriginalUrl.AbsoluteUri;
        CreatedAt = shortLink.CreatedAt;
        ExpiresAt = shortLink.ExpiresAt;
        ActiveFrom = shortLink.ActiveFrom;
        MaxClicks = shortLink.MaxClicks;
        ClickCount = shortLink.ClickCount;
        IsActive = shortLink.IsActive;
        CreatedByUserId = shortLink.CreatedByUserId;
        CreatedByDisplayName = shortLink.CreatedByDisplayName;
        CreatedByUsername = shortLink.CreatedByUsername;
        IdempotencyKey = shortLink.IdempotencyKey;
        TenantId = shortLink.TenantId ?? string.Empty;
        SharingMode = shortLink.SharingMode;
    }
}
