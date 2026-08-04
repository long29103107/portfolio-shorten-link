using ShortenLink.Core.Domain;

namespace ShortenLink.Core.Contracts.Responses;

public sealed record ResolveShortLinkResponse(
    bool Succeeded,
    ShortLink? ShortLink,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static ResolveShortLinkResponse Success(ShortLink shortLink) =>
        new(true, shortLink, null, null);

    public static ResolveShortLinkResponse Failure(string errorCode, string errorMessage) =>
        new(false, null, errorCode, errorMessage);
}
