namespace ShortenLink.Core.Contracts.Responses;

public sealed record DeactivateShortLinkResponse(
    bool Succeeded,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static DeactivateShortLinkResponse Success() => new(true, null, null);

    public static DeactivateShortLinkResponse Failure(string errorCode, string errorMessage) =>
        new(false, errorCode, errorMessage);
}
