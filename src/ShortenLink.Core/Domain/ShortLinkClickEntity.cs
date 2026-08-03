namespace ShortenLink.Core.Domain;

public sealed class ShortLinkClickEntity : BaseEntity<Guid>
{
    public ShortLinkClickEntity(
        string shortCode,
        DateTimeOffset clickedAtUtc,
        string? remoteIpAddress,
        string? userAgent,
        string? referrer,
        Guid? technicalId = null,
        string? tenantId = null)
        : base(clickedAtUtc, technicalId ?? Guid.CreateVersion7())
    {
        ShortCodeValidator.ValidateCodeOrThrow(shortCode);

        ShortCode = shortCode;
        ClickedAtUtc = clickedAtUtc;
        RemoteIpAddress = Normalize(remoteIpAddress);
        UserAgent = Normalize(userAgent);
        Referrer = Normalize(referrer);
        if (!ShortLinkTenantId.IsValid(tenantId))
        {
            throw new ArgumentException(
                $"Tenant identifier must be at most {ShortLinkTenantId.MaxLength} characters.",
                nameof(tenantId));
        }

        TenantId = ShortLinkTenantId.Normalize(tenantId);
    }

    public string ShortCode { get; }

    public DateTimeOffset ClickedAtUtc { get; }

    public string? RemoteIpAddress { get; }

    public string? UserAgent { get; }

    public string? Referrer { get; }

    public string? TenantId { get; }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
