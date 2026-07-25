using ShortenLink.Application.Abstractions;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Features.Security.Sessions;

public sealed record LoginSecurityUserCommand(
    string? Email,
    string? Username,
    string Password) : IRequest<SecurityLoginResponse>;

internal sealed class LoginSecurityUserCommandHandler(
    ISecuritySessionService sessionService)
    : IRequestHandler<LoginSecurityUserCommand, SecurityLoginResponse>
{
    public async Task<SecurityLoginResponse> Handle(
        LoginSecurityUserCommand request,
        CancellationToken cancellationToken)
    {
        var email = string.IsNullOrWhiteSpace(request.Email)
            ? request.Username
            : request.Email;
        var errors = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(email))
        {
            errors["email"] = ["Email is required."];
        }
        if (string.IsNullOrWhiteSpace(request.Password))
        {
            errors["password"] = ["Password is required."];
        }
        if (errors.Count > 0)
        {
            throw new AuthenticationRequiredException(
                ErrorCodes.InvalidLogin,
                "Email or password is invalid.",
                errors);
        }

        var result = await sessionService.LoginAsync(email!, request.Password, cancellationToken);
        return CreateResponse(result);
    }

    internal static SecurityLoginResponse CreateResponse(SecuritySessionResult result)
    {
        if (!result.Succeeded
            || result.User is null
            || string.IsNullOrWhiteSpace(result.AccessToken)
            || string.IsNullOrWhiteSpace(result.RefreshToken))
        {
            throw new AuthenticationRequiredException(
                result.ErrorCode ?? ErrorCodes.Unauthorized,
                result.ErrorMessage ?? "Email or password is invalid.");
        }

        return new SecurityLoginResponse(
            result.AccessToken,
            result.AccessToken,
            result.RefreshToken,
            new SecurityCurrentUserResponse(
                result.User.UserId,
                result.User.Username,
                result.User.DisplayName,
                result.User.Roles,
                result.User.Permissions,
                result.IssuedAtUtc ?? DateTimeOffset.UtcNow));
    }
}
