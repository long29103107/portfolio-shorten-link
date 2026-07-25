namespace ShortenLink.Core.Exceptions;

public sealed class ResourceGoneException(string errorCode, string message)
    : ShortenLinkException(errorCode, message);
