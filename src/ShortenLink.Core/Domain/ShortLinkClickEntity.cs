using ShortenLink.Core.Analytics;

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
        string? tenantId = null,
        string? device = null,
        string? browser = null,
        string? operatingSystem = null,
        string? countryCode = null,
        string? visitorKeyHash = null,
        ShortLinkClickMetadata? metadata = null)
        : base(clickedAtUtc, technicalId ?? Guid.CreateVersion7())
    {
        ShortCodeValidator.ValidateCodeOrThrow(shortCode);

        ShortCode = shortCode;
        ClickedAtUtc = clickedAtUtc;
        RemoteIpAddress = Normalize(remoteIpAddress);
        UserAgent = Normalize(userAgent);
        Referrer = Normalize(referrer);
        Device = Normalize(device ?? metadata?.Device);
        Browser = Normalize(browser ?? metadata?.Browser);
        OperatingSystem = Normalize(operatingSystem ?? metadata?.OperatingSystem);
        CountryCode = ShortLinkClickMetadataParser.NormalizeCountryCode(
            countryCode ?? metadata?.CountryCode);
        VisitorKeyHash = Normalize(visitorKeyHash ?? metadata?.VisitorKeyHash);
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

    public string? Device { get; }

    public string? Browser { get; }

    public string? OperatingSystem { get; }

    public string? CountryCode { get; }

    public string? VisitorKeyHash { get; }

    public string? TenantId { get; }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
