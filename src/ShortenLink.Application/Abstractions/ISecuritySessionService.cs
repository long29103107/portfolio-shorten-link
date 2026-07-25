namespace ShortenLink.Application.Abstractions;

public interface ISecuritySessionService
{
    Task<SecuritySessionResult> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    Task<SecuritySessionResult> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);
}

public sealed record SecuritySessionResult(
    bool Succeeded,
    CurrentUser? User,
    string? AccessToken,
    string? RefreshToken,
    DateTimeOffset? IssuedAtUtc,
    string? ErrorCode,
    string? ErrorMessage);
