namespace ShortenLink.Core.Contracts.Requests;

public sealed record ShortLinkImportItemRequest(
    string? OriginalUrl,
    DateTimeOffset? ExpiredAtUtc,
    string? IdempotencyKey = null,
    DateTimeOffset? ActiveFromUtc = null,
    int? MaxClicks = null);

public sealed record ShortLinkImportRequest(
    IReadOnlyList<ShortLinkImportItemRequest>? Items);
