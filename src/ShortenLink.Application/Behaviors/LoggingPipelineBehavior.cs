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
                $"Mediator request {requestName} completed in {stopwatch.ElapsedMilliseconds} ms.");
            return response;
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.WriteLine(
                $"Mediator request {requestName} failed after {stopwatch.ElapsedMilliseconds} ms: {exception.Message}");
            throw;
        }
    }
}
