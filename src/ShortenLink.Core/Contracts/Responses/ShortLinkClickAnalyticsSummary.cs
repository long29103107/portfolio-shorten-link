namespace ShortenLink.Core.Contracts.Responses;

public sealed record ShortLinkClickAnalyticsSummary(
    string ShortCode,
    long ClickCount,
    long UniqueClickCount,
    DateTimeOffset? LastClickedAtUtc,
    IReadOnlyList<ShortLinkClickDimensionSummary> Devices,
    IReadOnlyList<ShortLinkClickDimensionSummary> Browsers,
    IReadOnlyList<ShortLinkClickDimensionSummary> OperatingSystems,
    IReadOnlyList<ShortLinkClickDimensionSummary> Referrers,
    IReadOnlyList<ShortLinkClickDimensionSummary> Countries);

public sealed record ShortLinkClickDimensionSummary(
    string Name,
    long Count);
