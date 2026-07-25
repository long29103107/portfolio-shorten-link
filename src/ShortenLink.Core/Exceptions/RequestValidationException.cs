namespace ShortenLink.Core.Exceptions;

public sealed class RequestValidationException(
    string errorCode,
    string message,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? errors = null)
    : ShortenLinkException(errorCode, message)
{
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Errors { get; } =
        errors ?? new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
}
