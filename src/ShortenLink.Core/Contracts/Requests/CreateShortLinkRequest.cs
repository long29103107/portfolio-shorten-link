namespace ShortenLink.Core.Contracts.Requests;

public sealed record CreateShortLinkRequest(
    string OriginalUrl,
    DateTimeOffset? ExpiresAt = null,
    string? CreatedByUserId = null,
    string? CreatedByDisplayName = null,
    string? CreatedByUsername = null,
    string? IdempotencyKey = null);
