using System.Text.Json.Serialization;
using ShortenLink.Core.Domain;
using ShortenLink.Core.Security;

namespace ShortenLink.Application.Contracts.Responses;

public sealed record ShortLinkCreatedResponse(
    string Code,
    string ShortUrl,
    string OriginalUrl,
    DateTimeOffset CreatedAtUtc)
{
    public static ShortLinkCreatedResponse FromDomain(ShortLink shortLink, string shortUrl) =>
        new(shortLink.Code, shortUrl, shortLink.OriginalUrl.AbsoluteUri, shortLink.CreatedAt);
}

public sealed record ShortLinkAdminListItemResponse(
    string Code,
    string ShortUrl,
    string OriginalUrl,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ExpiredAtUtc,
    bool IsActive,
    string? CreatedByUserId,
    string? CreatedByDisplayName,
    string? CreatedByUsername,
    string? AccessLevel)
{
    public static ShortLinkAdminListItemResponse FromDomain(
        ShortLink shortLink,
        string shortUrl,
        string? accessLevel = null) =>
        new(
            shortLink.Code,
            shortUrl,
            shortLink.OriginalUrl.AbsoluteUri,
            shortLink.CreatedAt,
            shortLink.ExpiresAt,
            shortLink.IsActive,
            shortLink.CreatedByUserId,
            shortLink.CreatedByDisplayName,
            shortLink.CreatedByUsername,
            accessLevel);
}

public sealed record ShortLinkAdminListResponse(
    IReadOnlyList<ShortLinkAdminListItemResponse> Items,
    string? NextCursor,
    int? TotalCount = null,
    int? Page = null,
    int? PageSize = null,
    int? TotalPages = null);

public sealed record ShortLinkShareResponse(
    string UserId,
    string? Username,
    string? DisplayName,
    string Access,
    string CreatedByUserId,
    DateTimeOffset CreatedAtUtc)
{
    public static ShortLinkShareResponse FromDomain(
        ShortLinkShare share,
        ShortenLinkSecurityUser? user) =>
        new(
            share.UserId,
            user?.Username,
            user?.DisplayName,
            share.Access.ToString(),
            share.CreatedByUserId,
            share.CreatedAt);
}

public sealed record ShortLinkSharesResponse(
    string Mode,
    IReadOnlyList<ShortLinkShareResponse> Items);

public sealed record ShortLinkDetailsResponse(
    string Code,
    string OriginalUrl,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ExpiredAtUtc,
    bool IsActive)
{
    public static ShortLinkDetailsResponse FromDomain(ShortLink shortLink) =>
        new(
            shortLink.Code,
            shortLink.OriginalUrl.AbsoluteUri,
            shortLink.CreatedAt,
            shortLink.ExpiresAt,
            shortLink.IsActive);
}

public sealed record ShortLinkAnalyticsResponse(
    string Code,
    long ClickCount,
    DateTimeOffset? LastClickedAtUtc,
    IReadOnlyList<ShortLinkClickActivityResponse> RecentClicks)
{
    public static ShortLinkAnalyticsResponse FromClicks(
        string code,
        ShortLinkClickSummaryResponse summary,
        IReadOnlyList<ShortLinkClickEntity> recentClicks) =>
        new(
            code,
            summary.ClickCount,
            summary.LastClickedAtUtc,
            recentClicks.Select(ShortLinkClickActivityResponse.FromDomain).ToList());
}

public sealed record ShortLinkClickActivityResponse(
    DateTimeOffset ClickedAtUtc,
    string? RemoteIpAddress,
    string? UserAgent,
    string? Referrer)
{
    public static ShortLinkClickActivityResponse FromDomain(ShortLinkClickEntity click) =>
        new(click.ClickedAtUtc, click.RemoteIpAddress, click.UserAgent, click.Referrer);
}

public sealed record SecurityLoginResponse(
    string Token,
    string AccessToken,
    string RefreshToken,
    SecurityCurrentUserResponse User);

public sealed record SecurityCurrentUserResponse(
    string UserId,
    string Username,
    string DisplayName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    DateTimeOffset IssuedAtUtc);

public sealed record SecurityUserApiKeysListResponse(
    IReadOnlyList<SecurityUserApiKeyResponse> Items);

public sealed record SecurityUserApiKeyCreatedResponse(
    SecurityUserApiKeyResponse ApiKey,
    string RawApiKey);

public sealed record SecurityUserApiKeyResponse(
    string Id,
    string DisplayName,
    bool IsEnabled,
    DateTimeOffset CreatedAtUtc)
{
    public static SecurityUserApiKeyResponse FromDomain(ShortenLinkUserApiKey apiKey) =>
        new(apiKey.ApiKeyKey, apiKey.DisplayName, apiKey.IsEnabled, apiKey.CreatedAt);
}

public sealed record SecurityUserApiKeyDisabledResponse(string Id, bool IsEnabled);

public sealed record SecurityRolesListResponse(
    IReadOnlyList<SecurityRoleResponse> SystemRoles,
    IReadOnlyList<SecurityRoleResponse> CustomRoles);

public sealed record SecurityRoleResponse(
    string Id,
    string Name,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> DefaultPermissions,
    IReadOnlyList<SecurityRolePermissionOverrideResponse> PermissionOverrides,
    bool IsSystem,
    bool IsEnabled,
    bool CanDelete,
    DateTimeOffset? CreatedAtUtc)
{
    public static SecurityRoleResponse System(
        string id,
        IEnumerable<string> permissions,
        IReadOnlyList<ShortenLinkRolePermissionOverride> overrides)
    {
        var defaults = permissions.OrderBy(static permission => permission, StringComparer.Ordinal).ToList();
        return new(
            id,
            id,
            ApplyOverrides(defaults, overrides),
            defaults,
            overrides.Select(SecurityRolePermissionOverrideResponse.FromDomain).ToList(),
            IsSystem: true,
            IsEnabled: true,
            CanDelete: false,
            CreatedAtUtc: null);
    }

    public static SecurityRoleResponse Custom(
        ShortenLinkCustomRole role,
        IReadOnlyList<ShortenLinkRolePermissionOverride> overrides) =>
        new(
            role.RoleKey,
            role.Name,
            ApplyOverrides(role.Permissions, overrides),
            role.Permissions,
            overrides.Select(SecurityRolePermissionOverrideResponse.FromDomain).ToList(),
            IsSystem: false,
            role.IsEnabled,
            CanDelete: true,
            role.CreatedAt);

    private static IReadOnlyList<string> ApplyOverrides(
        IEnumerable<string> defaults,
        IEnumerable<ShortenLinkRolePermissionOverride> overrides)
    {
        var effective = new HashSet<string>(defaults, StringComparer.Ordinal);
        foreach (var item in overrides)
        {
            if (item.IsAllowed) effective.Add(item.Permission);
            else effective.Remove(item.Permission);
        }

        return effective.OrderBy(static permission => permission, StringComparer.Ordinal).ToList();
    }
}

public sealed record SecurityRolePermissionOverrideResponse(string Permission, bool IsAllowed)
{
    public static SecurityRolePermissionOverrideResponse FromDomain(ShortenLinkRolePermissionOverride item) =>
        new(item.Permission, item.IsAllowed);
}

public sealed record SecurityRoleDeletedResponse(string Id);

public sealed record SecurityUsersListResponse(
    IReadOnlyList<SecurityUserResponse> Items);

public sealed record SecurityUserResponse(
    string Id,
    string Username,
    string DisplayName,
    IReadOnlyList<string> RoleIds,
    bool IsEnabled,
    bool IsHidden,
    bool IsBootstrap,
    DateTimeOffset CreatedAtUtc)
{
    public static SecurityUserResponse FromDomain(ShortenLinkSecurityUser user) =>
        new(
            user.UserKey,
            user.Username,
            user.DisplayName,
            user.RoleIds,
            user.IsEnabled,
            user.IsHidden,
            user.IsBootstrap,
            user.CreatedAt);
}

public sealed record SecurityUserDisabledResponse(string Id, bool IsEnabled);

public sealed record SecurityAssignmentsListResponse(
    IReadOnlyList<SecurityAssignmentResponse> Items);

public sealed record SecurityAssignmentResponse(
    string CredentialKeyHash,
    string Name,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    bool IsEnabled,
    DateTimeOffset CreatedAtUtc)
{
    public static SecurityAssignmentResponse FromDomain(ShortenLinkSecurityAssignment assignment) =>
        new(
            assignment.CredentialKeyHash,
            assignment.Name,
            assignment.Roles,
            assignment.Permissions,
            assignment.IsEnabled,
            assignment.CreatedAt);
}

public sealed record SecurityAssignmentDisabledResponse(
    string CredentialKeyHash,
    bool IsEnabled);

public sealed record ShortLinkDeactivatedResponse(string Code, bool IsActive);

public sealed record ShortLinkDeletedResponse(string Code);

public sealed record ShortLinkAuditEventsResponse(
    IReadOnlyList<ShortLinkAuditEventResponse> Items,
    string? NextCursor);

public sealed record ShortLinkAuditActionsResponse(IReadOnlyList<string> Items);

public sealed record ShortLinkAuditEventResponse(
    Guid Id,
    string ActorId,
    string Action,
    string TargetType,
    string TargetId,
    string? OwnerUserId,
    string Outcome,
    DateTimeOffset OccurredAtUtc,
    string? SubjectUserId,
    string? Detail)
{
    public static ShortLinkAuditEventResponse FromDomain(ShortLinkAuditEvent auditEvent) =>
        new(
            auditEvent.Id,
            auditEvent.ActorId,
            auditEvent.Action,
            auditEvent.TargetType,
            auditEvent.TargetId,
            auditEvent.OwnerUserId,
            auditEvent.Outcome,
            auditEvent.OccurredAt,
            auditEvent.SubjectUserId,
            auditEvent.Detail);
}

public sealed record ShortLinkErrorResponse(
    string ErrorCode,
    string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyDictionary<string, IReadOnlyList<string>>? FieldErrors = null);
