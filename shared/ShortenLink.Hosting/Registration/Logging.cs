using Microsoft.Extensions.Logging;
using ShortenLink.Application.Diagnostics;

namespace ShortenLink.Hosting;

internal sealed class StructuredRequestLogger(ILogger<StructuredRequestLogger> logger) : IRequestLogger
{
    private static readonly Action<ILogger, string, long, Exception?> completed =
        LoggerMessage.Define<string, long>(
            LogLevel.Information,
            new EventId(2001, "ShortenLinkRequestCompleted"),
            "ShortenLinkRequestCompleted request={Request} elapsed_ms={ElapsedMilliseconds}.");

    private static readonly Action<ILogger, string, long, string, Exception?> failed =
        LoggerMessage.Define<string, long, string>(
            LogLevel.Error,
            new EventId(2002, "ShortenLinkRequestFailed"),
            "ShortenLinkRequestFailed request={Request} elapsed_ms={ElapsedMilliseconds} exception_type={ExceptionType}.");

    public void RequestCompleted(string requestName, long elapsedMilliseconds) =>
        completed(logger, requestName, elapsedMilliseconds, null);

    public void RequestFailed(string requestName, long elapsedMilliseconds, Type exceptionType) =>
        failed(logger, requestName, elapsedMilliseconds, exceptionType.Name, null);
}
