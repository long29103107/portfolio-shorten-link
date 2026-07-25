namespace ShortenLink.Application.Abstractions;

public interface ICurrentRequestContext
{
    Task EnsureAuthorizedAsync(
        string permission,
        CancellationToken cancellationToken = default);

    Task<CurrentRequestActor> AuthorizeAsync(
        string permission,
        CancellationToken cancellationToken = default);

    Task<CurrentUser?> GetCurrentUserAsync(
        CancellationToken cancellationToken = default);
}

public sealed record CurrentUser(
    string UserId,
    string Username,
    string DisplayName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);

public sealed record CurrentRequestActor(
    string? UserId,
    bool IsAdmin,
    string? ActorId = null);
