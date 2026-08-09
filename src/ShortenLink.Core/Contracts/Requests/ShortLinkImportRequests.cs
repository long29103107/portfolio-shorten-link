namespace ShortenLink.Core.Contracts.Requests;

public sealed record ShortLinkImportItemRequest(
    string? OriginalUrl,
    DateTimeOffset? ExpiredAtUtc,
    string? IdempotencyKey = null,
    DateTimeOffset? ActiveFromUtc = null);

public sealed record ShortLinkImportRequest(
    IReadOnlyList<ShortLinkImportItemRequest>? Items);
