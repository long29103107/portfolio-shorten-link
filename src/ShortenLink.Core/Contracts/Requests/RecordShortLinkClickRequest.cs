namespace ShortenLink.Core.Contracts.Requests;

public sealed record RecordShortLinkClickRequest(
    string ShortCode,
    DateTimeOffset ClickedAtUtc,
    string? RemoteIpAddress,
    string? UserAgent,
    string? Referrer,
    string? TenantId = null,
    string? Device = null,
    string? Browser = null,
    string? OperatingSystem = null,
    string? CountryCode = null,
    string? VisitorKeyHash = null);
