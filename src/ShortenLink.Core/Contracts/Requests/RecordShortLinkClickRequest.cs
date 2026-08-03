namespace ShortenLink.Core.Contracts.Requests;

public sealed record RecordShortLinkClickRequest(
    string ShortCode,
    DateTimeOffset ClickedAtUtc,
    string? RemoteIpAddress,
    string? UserAgent,
    string? Referrer,
    string? TenantId = null);
