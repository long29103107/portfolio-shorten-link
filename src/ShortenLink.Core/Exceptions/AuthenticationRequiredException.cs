namespace ShortenLink.Core.Exceptions;

public sealed class AuthenticationRequiredException(
    string errorCode = ErrorCodes.Unauthorized,
    string message = "A valid credential is required.",
    IReadOnlyDictionary<string, IReadOnlyList<string>>? errors = null)
    : ShortenLinkException(errorCode, message)
{
    public IReadOnlyDictionary<string, IReadOnlyList<string>>? Errors { get; } = errors;
}
