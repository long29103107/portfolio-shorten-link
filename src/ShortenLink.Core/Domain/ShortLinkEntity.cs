using ShortenLink.Core.Security;

namespace ShortenLink.Core.Domain;

public sealed class ShortLinkEntity : BaseEntity<Guid>
{
    public ShortLinkEntity(
        string code,
        Uri originalUrl,
        DateTimeOffset createdAt,
        DateTimeOffset? expiresAt = null,
        bool isActive = true,
        string? createdByUserId = null,
        string? createdByDisplayName = null,
        string? createdByUsername = null,
        Guid? technicalId = null,
        string? idempotencyKey = null,
        string? tenantId = null,
        ShortLinkSharingMode sharingMode = ShortLinkSharingMode.AllowList,
        DateTimeOffset? activeFrom = null,
        int? maxClicks = null,
        int clickCount = 0,
        string? passwordHash = null)
        : base(createdAt, technicalId ?? Guid.CreateVersion7())
    {
        ShortCodeValidator.ValidateCodeOrThrow(code);
        ArgumentNullException.ThrowIfNull(originalUrl);

        if (!ShortLinkUrlValidator.IsValid(originalUrl.AbsoluteUri))
        {
            throw new ArgumentException("Original URL must be an absolute HTTP or HTTPS URL.", nameof(originalUrl));
        }

        if (maxClicks is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxClicks), "MaxClicks must be a positive integer.");
        }

        if (clickCount < 0 || maxClicks is not null && clickCount > maxClicks)
        {
            throw new ArgumentOutOfRangeException(nameof(clickCount), "ClickCount must be within the configured click limit.");
        }

        Code = code;
        OriginalUrl = originalUrl;
        ExpiresAt = expiresAt;
        IsActive = isActive;
        CreatedByUserId = Normalize(createdByUserId);
        CreatedByDisplayName = Normalize(createdByDisplayName);
        CreatedByUsername = Normalize(createdByUsername);
        IdempotencyKey = Normalize(idempotencyKey);
        if (!ShortLinkTenantId.IsValid(tenantId))
        {
            throw new ArgumentException(
                $"Tenant identifier must be at most {ShortLinkTenantId.MaxLength} characters.",
                nameof(tenantId));
        }

        TenantId = ShortLinkTenantId.Normalize(tenantId);
        SharingMode = sharingMode;
        ActiveFrom = activeFrom;
        MaxClicks = maxClicks;
        ClickCount = clickCount;
        PasswordHash = Normalize(passwordHash);
    }

    public string Code { get; }

    public Uri OriginalUrl { get; }

    public DateTimeOffset? ExpiresAt { get; }

    public DateTimeOffset? ActiveFrom { get; }

    public int? MaxClicks { get; }

    public int ClickCount { get; }

    public string? PasswordHash { get; }

    public bool IsPasswordProtected => PasswordHash is not null;

    public bool IsActive { get; private set; }

    public string? CreatedByUserId { get; }

    public string? CreatedByDisplayName { get; }

    public string? CreatedByUsername { get; }

    public string? IdempotencyKey { get; }

    public string? TenantId { get; }

    public ShortLinkSharingMode SharingMode { get; private set; }

    public bool IsExpired(DateTimeOffset now) => ExpiresAt is not null && ExpiresAt <= now;

    public bool IsScheduled(DateTimeOffset now) => ActiveFrom is not null && ActiveFrom > now;

    public bool IsClickLimitReached => MaxClicks is not null && ClickCount >= MaxClicks;

    public bool VerifyPassword(string password) =>
        PasswordHash is not null
        && ShortenLinkSecurityCredentialHasher.VerifyPassword(password, PasswordHash);

    public bool CanResolve(DateTimeOffset now) => IsActive && !IsScheduled(now) && !IsExpired(now);

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public void SetSharingMode(ShortLinkSharingMode sharingMode) => SharingMode = sharingMode;

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
