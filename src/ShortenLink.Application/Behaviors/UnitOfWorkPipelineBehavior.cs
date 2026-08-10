using ShortenLink.Application.Abstractions;
using ShortenLink.Application.Features.Audit;
using ShortenLink.Core.Abstractions;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Behaviors;

public sealed class UnitOfWorkPipelineBehavior<TRequest, TResponse>(
    IUnitOfWork unitOfWork,
    AuditEventBuffer eventBuffer,
    IAuditEventQueue auditEventQueue)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is IBypassUnitOfWork)
            return await next();

        try
        {
            var response = await unitOfWork.ExecuteAsync(_ => next(), cancellationToken);
            foreach (var auditEvent in eventBuffer.Drain())
            {
                try
                {
                    await auditEventQueue.EnqueueAsync(auditEvent, cancellationToken);
                }
                catch
                {
                    // Audit delivery is intentionally fail-open. The business
                    // transaction has already committed at this point.
                }
            }

            return response;
        }
        catch
        {
            eventBuffer.Clear();
            throw;
        }
    }
}
