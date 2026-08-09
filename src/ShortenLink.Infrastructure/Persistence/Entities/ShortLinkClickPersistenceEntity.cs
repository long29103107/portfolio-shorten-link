namespace ShortenLink.Infrastructure.Persistence.Entities;

public sealed class ShortLinkClickPersistenceEntity : BaseEntity<Guid>
{
    public string ShortCode { get; set; } = string.Empty;

    public string TenantId { get; set; } = string.Empty;

    public DateTimeOffset ClickedAtUtc { get; set; }

    public string? RemoteIpAddress { get; set; }

    public string? UserAgent { get; set; }

    public string? Referrer { get; set; }

    public string? Device { get; set; }

    public string? Browser { get; set; }

    public string? OperatingSystem { get; set; }

    public string? CountryCode { get; set; }

    public string? VisitorKeyHash { get; set; }

    public static ShortLinkClickPersistenceEntity FromDomain(ShortLinkClick shortLinkClick)
    {
        ArgumentNullException.ThrowIfNull(shortLinkClick);

        return new ShortLinkClickPersistenceEntity
        {
            Id = shortLinkClick.Id,
            CreatedAt = shortLinkClick.CreatedAt,
            ShortCode = shortLinkClick.ShortCode,
            TenantId = shortLinkClick.TenantId ?? string.Empty,
            ClickedAtUtc = shortLinkClick.ClickedAtUtc,
            RemoteIpAddress = shortLinkClick.RemoteIpAddress,
            UserAgent = shortLinkClick.UserAgent,
            Referrer = shortLinkClick.Referrer,
            Device = shortLinkClick.Device,
            Browser = shortLinkClick.Browser,
            OperatingSystem = shortLinkClick.OperatingSystem,
            CountryCode = shortLinkClick.CountryCode,
            VisitorKeyHash = shortLinkClick.VisitorKeyHash
        };
    }

    public ShortLinkClick ToDomain() =>
        new(
            ShortCode,
            ClickedAtUtc,
            RemoteIpAddress,
            UserAgent,
            Referrer,
            Id,
            TenantId,
            Device,
            Browser,
            OperatingSystem,
            CountryCode,
            VisitorKeyHash);
}
