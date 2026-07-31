using System.Diagnostics;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Behaviors;

public sealed class LoggingPipelineBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await next();
            System.Diagnostics.Trace.WriteLine(
                $"ShortenLinkRequestCompleted request={requestName} elapsed_ms={stopwatch.ElapsedMilliseconds}.");
            return response;
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.WriteLine(
                $"ShortenLinkRequestFailed request={requestName} elapsed_ms={stopwatch.ElapsedMilliseconds} exception_type={exception.GetType().Name}.");
            throw;
        }
    }
}
