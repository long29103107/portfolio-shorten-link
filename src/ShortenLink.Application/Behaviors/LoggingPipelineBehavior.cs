using System.Diagnostics;
using ShortenLink.Application.Diagnostics;
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
            RequestDiagnostics.RecordCompleted(requestName, stopwatch.ElapsedMilliseconds);
            return response;
        }
        catch (Exception exception)
        {
            RequestDiagnostics.RecordFailed(requestName, stopwatch.ElapsedMilliseconds, exception.GetType());
            throw;
        }
    }
}
