namespace ShortenLink.Core.Contracts.Responses;

public sealed record ShortLinkClickSummary(
    string ShortCode,
    long ClickCount,
    DateTimeOffset? LastClickedAtUtc);
