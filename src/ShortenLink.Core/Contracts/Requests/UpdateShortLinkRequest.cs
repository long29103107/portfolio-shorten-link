namespace ShortenLink.Core.Contracts.Requests;

public sealed record UpdateShortLinkRequest(
    string OriginalUrl,
    DateTimeOffset? ExpiresAt = null,
    DateTimeOffset? ActiveFrom = null);
