namespace ShortenLink.Core.Exceptions;

/// <summary>
/// Signals that another writer persisted the same idempotency key.
/// </summary>
public sealed class ShortLinkIdempotencyConflictException : Exception
{
    public ShortLinkIdempotencyConflictException(Exception? innerException = null)
        : base("The idempotency key already exists.", innerException)
    {
    }
}
