namespace ShortenLink.Core.Contracts.Requests;

public sealed record CreateShortLinkRequest(
    string OriginalUrl,
    DateTimeOffset? ExpiresAt = null,
    string? CreatedByUserId = null,
    string? CreatedByDisplayName = null,
    string? CreatedByUsername = null,
    string? IdempotencyKey = null,
    string? TenantId = null,
    DateTimeOffset? ActiveFrom = null,
    int? MaxClicks = null,
    string? Password = null,
    string? Folder = null,
    IReadOnlyList<string>? Tags = null);
