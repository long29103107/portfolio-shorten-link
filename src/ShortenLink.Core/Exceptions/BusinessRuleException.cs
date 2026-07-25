namespace ShortenLink.Core.Exceptions;

public sealed class BusinessRuleException(string errorCode, string message)
    : ShortenLinkException(errorCode, message);
