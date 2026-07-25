namespace ShortenLink.Core.Exceptions;

public sealed class ForbiddenException(
    string errorCode = ErrorCodes.Forbidden,
    string message = "The request is not authorized.")
    : ShortenLinkException(errorCode, message);
