using Microsoft.AspNetCore.Diagnostics;
using ShortenLink.Core.Services;

namespace ShortenLink.Api;

internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, response) = exception switch
        {
            RequestValidationException validationException =>
                (StatusCodes.Status400BadRequest, CreateValidationResponse(validationException)),
            AuthenticationRequiredException authenticationException =>
                (StatusCodes.Status401Unauthorized, new ShortLinkErrorResponse(
                    authenticationException.ErrorCode,
                    authenticationException.Message,
                    authenticationException.Errors)),
            ForbiddenException forbiddenException =>
                (StatusCodes.Status403Forbidden, CreateResponse(forbiddenException)),
            NotFoundException notFoundException =>
                (StatusCodes.Status404NotFound, CreateResponse(notFoundException)),
            ConflictException conflictException =>
                (StatusCodes.Status409Conflict, CreateResponse(conflictException)),
            ResourceGoneException goneException =>
                (StatusCodes.Status410Gone, CreateResponse(goneException)),
            BusinessRuleException businessException =>
                (StatusCodes.Status400BadRequest, CreateResponse(businessException)),
            _ => (
                StatusCodes.Status500InternalServerError,
                new ShortLinkErrorResponse(ErrorCodes.InternalError, "An unexpected error occurred."))
        };

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled request exception.");
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response
            .WriteAsJsonAsync(response, cancellationToken)
            ;
        return true;
    }

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

        if (errorCode == ShortLinkErrorCodes.InvalidPassword)
        {
            return new Dictionary<string, IReadOnlyList<string>>
            {
                ["password"] = [message]
            };
        }

        if (errorCode == ShortLinkErrorCodes.InvalidFolder)
        {
            return new Dictionary<string, IReadOnlyList<string>>
            {
                ["folder"] = [message]
            };
        }

        if (errorCode == ShortLinkErrorCodes.InvalidTags)
        {
            return new Dictionary<string, IReadOnlyList<string>>
            {
                ["tags"] = [message]
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

}
