namespace ShortenLink.Core.Exceptions;

/// <summary>
/// Signals that a generated short code conflicted with an existing persisted code.
/// Providers should use this exception to allow the Application layer to retry
/// with a new candidate without depending on provider-specific exceptions.
/// </summary>
public sealed class ShortLinkCodeConflictException : Exception
{
    public ShortLinkCodeConflictException(string code, Exception? innerException = null)
        : base($"Short code '{code}' already exists.", innerException)
    {
        Code = string.IsNullOrWhiteSpace(code)
            ? throw new ArgumentException("A short code is required.", nameof(code))
            : code;
    }

    public string Code { get; }
}
