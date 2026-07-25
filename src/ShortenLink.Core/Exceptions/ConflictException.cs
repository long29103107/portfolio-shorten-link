namespace ShortenLink.Core.Exceptions;

public sealed class ConflictException(string errorCode, string message)
    : ShortenLinkException(errorCode, message);
