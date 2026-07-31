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
        string? idempotencyKey = null)
        : base(createdAt, technicalId ?? Guid.CreateVersion7())
    {
        ShortCodeValidator.ValidateCodeOrThrow(code);
        ArgumentNullException.ThrowIfNull(originalUrl);

        if (!ShortLinkUrlValidator.IsValid(originalUrl.AbsoluteUri))
        {
            throw new ArgumentException("Original URL must be an absolute HTTP or HTTPS URL.", nameof(originalUrl));
        }

        Code = code;
        OriginalUrl = originalUrl;
        ExpiresAt = expiresAt;
        IsActive = isActive;
        CreatedByUserId = Normalize(createdByUserId);
        CreatedByDisplayName = Normalize(createdByDisplayName);
        CreatedByUsername = Normalize(createdByUsername);
        IdempotencyKey = Normalize(idempotencyKey);
    }

    public string Code { get; }

    public Uri OriginalUrl { get; }

    public DateTimeOffset? ExpiresAt { get; }

    public bool IsActive { get; private set; }

    public string? CreatedByUserId { get; }

    public string? CreatedByDisplayName { get; }

    public string? CreatedByUsername { get; }

    public string? IdempotencyKey { get; }

    public bool IsExpired(DateTimeOffset now) => ExpiresAt is not null && ExpiresAt <= now;

    public bool CanResolve(DateTimeOffset now) => IsActive && !IsExpired(now);

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
