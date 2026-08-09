namespace ShortenLink.Core.Contracts.Requests;

public sealed record UpdateShortLinkRequest(
    string OriginalUrl,
    DateTimeOffset? ExpiresAt = null,
    DateTimeOffset? ActiveFrom = null,
    int? MaxClicks = null,
    string? Password = null,
    bool ClearPassword = false,
    string? Folder = null,
    IReadOnlyList<string>? Tags = null);
