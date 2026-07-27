using ShortenLink.Core.Abstractions;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Behaviors;

public sealed class UnitOfWorkPipelineBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync(_ => next(), cancellationToken);
}
