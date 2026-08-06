namespace ShortenLink.Application.Diagnostics;

/// <summary>
/// Host-neutral request diagnostic seam. Implementations must log only the
/// request type, elapsed time, and exception type; request payloads are never
/// part of this contract.
/// </summary>
public interface IRequestLogger
{
    void RequestCompleted(string requestName, long elapsedMilliseconds);

    void RequestFailed(string requestName, long elapsedMilliseconds, Type exceptionType);
}

internal sealed class NullRequestLogger : IRequestLogger
{
    public void RequestCompleted(string requestName, long elapsedMilliseconds)
    {
    }

    public void RequestFailed(string requestName, long elapsedMilliseconds, Type exceptionType)
    {
    }
}
