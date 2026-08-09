using ShortenLink.Core.Domain;

namespace ShortenLink.Application.Contracts.Responses;

public sealed record ShortLinkExportRecord(
    string Code,
    string OriginalUrl,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ExpiredAtUtc,
    bool IsActive,
    string AccessLevel,
    DateTimeOffset? ActiveFromUtc = null)
{
    public static ShortLinkExportRecord FromDomain(ShortLink shortLink, string accessLevel) =>
        new(
            shortLink.Code,
            shortLink.OriginalUrl.AbsoluteUri,
            shortLink.CreatedAt,
            shortLink.ExpiresAt,
            shortLink.IsActive,
            accessLevel,
            shortLink.ActiveFrom);
}
