using System.Diagnostics;

namespace ShortenLink.Application.Diagnostics;

/// <summary>
/// Keeps the existing mediator request diagnostic text in one internal seam.
/// This deliberately remains Trace-based until the structured logging task
/// defines a host-level logging contract.
/// </summary>
internal static class RequestDiagnostics
{
    public static void RecordCompleted(string requestName, long elapsedMilliseconds) =>
        Trace.WriteLine(
            $"ShortenLinkRequestCompleted request={requestName} elapsed_ms={elapsedMilliseconds}.");

    public static void RecordFailed(
        string requestName,
        long elapsedMilliseconds,
        Type exceptionType) =>
        Trace.WriteLine(
            $"ShortenLinkRequestFailed request={requestName} elapsed_ms={elapsedMilliseconds} exception_type={exceptionType.Name}.");
}
