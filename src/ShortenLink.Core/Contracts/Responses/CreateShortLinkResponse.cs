using ShortenLink.Core.Domain;

namespace ShortenLink.Core.Contracts.Responses;

public sealed record CreateShortLinkResponse(
    bool Succeeded,
    ShortLink? ShortLink,
    string? ErrorCode,
    string? ErrorMessage,
    bool Replayed = false)
{
    public static CreateShortLinkResponse Success(ShortLink shortLink) =>
        new(true, shortLink, null, null);

    public static CreateShortLinkResponse Replay(ShortLink shortLink) =>
        new(true, shortLink, null, null, true);

    public static CreateShortLinkResponse Failure(string errorCode, string errorMessage) =>
        new(false, null, errorCode, errorMessage);
}
