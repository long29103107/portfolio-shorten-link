namespace ShortenLink.Core.Exceptions;

public sealed class NotFoundException(string errorCode, string message)
    : ShortenLinkException(errorCode, message);
