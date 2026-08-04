namespace ShortenLink.Core.Contracts.Responses;

public sealed record ShortLinkClickSummaryResponse(
    string ShortCode,
    long ClickCount,
    DateTimeOffset? LastClickedAtUtc);
