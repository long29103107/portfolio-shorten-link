using FluentValidation.Results;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Behaviors;

public sealed class ValidationPipelineBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var validationResults = await Task.WhenAll(
            validators.Select(validator => validator.ValidateAsync(request, cancellationToken)));
        var failures = validationResults
            .SelectMany(static result => result.Errors)
            .Where(static failure => failure is not null)
            .ToArray();

        if (failures.Length > 0)
        {
            throw CreateException(failures);
        }

        return await next();
    }

    private static RequestValidationException CreateException(
        IReadOnlyList<ValidationFailure> failures)
    {
        var errors = failures
            .GroupBy(
                static failure => NormalizePropertyName(failure.PropertyName),
                StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => (IReadOnlyList<string>)group
                    .Select(static failure => failure.ErrorMessage)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);
        var firstFailure = failures[0];
        var errorCode = string.IsNullOrWhiteSpace(firstFailure.ErrorCode)
            ? ErrorCodes.InvalidRequest
            : firstFailure.ErrorCode;

        return new RequestValidationException(
            errorCode,
            firstFailure.ErrorMessage,
            errors);
    }

    private static string NormalizePropertyName(string propertyName) =>
        string.IsNullOrEmpty(propertyName) || char.IsLower(propertyName[0])
            ? propertyName
            : string.Concat(char.ToLowerInvariant(propertyName[0]), propertyName[1..]);
}
