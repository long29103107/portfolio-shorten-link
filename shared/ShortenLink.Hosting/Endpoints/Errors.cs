using Microsoft.AspNetCore.Http;
using ShortenLink.Application.Contracts.Responses;
using ShortenLink.Core.Services;

namespace ShortenLink.Hosting;

internal sealed class ShortenLinkExceptionEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (ShortenLinkException exception)
        {
            var mapping = exception switch
            {
                RequestValidationException validationException =>
                    CreateMapping(StatusCodes.Status400BadRequest, CreateValidationResponse(validationException)),
                AuthenticationRequiredException authenticationException =>
                    CreateMapping(StatusCodes.Status401Unauthorized, new ShortLinkErrorResponse(
                        authenticationException.ErrorCode,
                        authenticationException.Message,
                        authenticationException.Errors)),
                ForbiddenException forbiddenException =>
                    CreateMapping(StatusCodes.Status403Forbidden, CreateResponse(forbiddenException)),
                NotFoundException notFoundException =>
                    CreateMapping(StatusCodes.Status404NotFound, CreateResponse(notFoundException)),
                ConflictException conflictException =>
                    CreateMapping(StatusCodes.Status409Conflict, CreateResponse(conflictException)),
                ResourceGoneException goneException =>
                    CreateMapping(StatusCodes.Status410Gone, CreateResponse(goneException)),
                BusinessRuleException businessException =>
                    CreateMapping(StatusCodes.Status400BadRequest, CreateResponse(businessException)),
                _ => null
            };

            if (mapping is null)
            {
                throw;
            }

            return Results.Json(mapping.Response, statusCode: mapping.StatusCode);
        }
    }

    private static ExceptionMapping CreateMapping(
        int statusCode,
        ShortLinkErrorResponse response) =>
        new(statusCode, response);

    private static ShortLinkErrorResponse CreateResponse(ShortenLinkException exception) =>
        new(exception.ErrorCode, exception.Message);

    private static ShortLinkErrorResponse CreateValidationResponse(RequestValidationException exception) =>
        new(
            exception.ErrorCode,
            exception.Message,
            exception.Errors.Count > 0
                ? exception.Errors
                : GetFieldErrors(exception.ErrorCode, exception.Message));

    private static IReadOnlyDictionary<string, IReadOnlyList<string>>? GetFieldErrors(
        string errorCode,
        string message)
    {
        if (errorCode == ShortLinkErrorCodes.InvalidActivationWindow)
        {
            return new Dictionary<string, IReadOnlyList<string>>
            {
                ["activeFromUtc"] = [message],
                ["expiredAtUtc"] = [message]
            };
        }

        if (errorCode == ShortLinkErrorCodes.InvalidMaxClicks)
        {
            return new Dictionary<string, IReadOnlyList<string>>
            {
                ["maxClicks"] = [message]
            };
        }

        var field = errorCode switch
        {
            ShortLinkErrorCodes.InvalidUrl => "originalUrl",
            ShortLinkErrorCodes.InvalidExpiration => "expiredAtUtc",
            _ => null
        };

        return field is null
            ? null
            : new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                [field] = [message]
            };
    }

    private sealed record ExceptionMapping(int StatusCode, ShortLinkErrorResponse Response);
}
