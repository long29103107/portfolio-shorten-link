using ShortenLink.Application.Abstractions;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Behaviors;

public sealed class ValidationPipelineBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is IRequestValidation validation)
        {
            validation.Validate();
        }

        return next();
    }
}
