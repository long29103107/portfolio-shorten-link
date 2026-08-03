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

    /// <summary>
    /// Returns trusted optional tenant context for public tenant-aware operations.
    /// The default implementation preserves single-tenant hosts.
    /// </summary>
    Task<string?> GetCurrentTenantIdAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);
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
    string? ActorId = null,
    string? TenantId = null);
