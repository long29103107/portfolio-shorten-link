using ShortenLink.Core.Domain;

namespace ShortenLink.Core.Contracts.Responses;

public sealed record ShortLinkDetailsResponse(
    bool Succeeded,
    ShortLink? ShortLink,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static ShortLinkDetailsResponse Success(ShortLink shortLink) =>
        new(true, shortLink, null, null);

    public static ShortLinkDetailsResponse Failure(string errorCode, string errorMessage) =>
        new(false, null, errorCode, errorMessage);
}
