namespace ShortenLink.Core.Exceptions;

public abstract class ShortenLinkException(
    string errorCode,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    public string ErrorCode { get; } =
        string.IsNullOrWhiteSpace(errorCode) ? "request_failed" : errorCode;
}
