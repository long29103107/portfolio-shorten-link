using ShortenLink.Core.Domain;

namespace ShortenLink.Application.Contracts.Responses;

public sealed record ShortLinkExportRecord(
    string Code,
    string OriginalUrl,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ExpiredAtUtc,
    bool IsActive,
    string AccessLevel,
    DateTimeOffset? ActiveFromUtc = null,
    int? MaxClicks = null,
    int ClickCount = 0,
    bool IsPasswordProtected = false,
    string? Folder = null,
    IReadOnlyList<string>? Tags = null)
{
    public static ShortLinkExportRecord FromDomain(ShortLink shortLink, string accessLevel) =>
        new(
            shortLink.Code,
            shortLink.OriginalUrl.AbsoluteUri,
            shortLink.CreatedAt,
            shortLink.ExpiresAt,
            shortLink.IsActive,
            accessLevel,
            shortLink.ActiveFrom,
            shortLink.MaxClicks,
            shortLink.ClickCount,
            shortLink.IsPasswordProtected,
            shortLink.Folder,
            shortLink.Tags);
}
