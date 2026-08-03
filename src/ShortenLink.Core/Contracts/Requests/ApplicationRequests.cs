namespace ShortenLink.Core.Contracts.Requests;

public sealed record ShortLinkCreateRequest(
    string OriginalUrl,
    DateTimeOffset? ExpiredAtUtc);

public sealed record ShortLinkUpdateRequest(
    string OriginalUrl,
    DateTimeOffset? ExpiredAtUtc);

public sealed record ShortLinkShareUpsertRequest(
    string Username,
    string Access);

public sealed record ShortLinkSharingModeRequest(string Mode);

public sealed record ShortLinkExpirationExecutionRequestDto(
    DateTimeOffset? EvaluatedAtUtc,
    int? Limit,
    double? RetainExpiredForSeconds,
    bool? ResumeFromCheckpoint);

public sealed record SecurityAssignmentUpsertRequest(
    string Name,
    string CredentialKey,
    IReadOnlyList<string>? Roles,
    IReadOnlyList<string>? Permissions,
    bool? IsEnabled);

public sealed record SecurityLoginRequest(
    string? Email,
    string Password,
    string? Username = null);

public sealed record SecurityRefreshRequest(string RefreshToken);

public sealed record SecurityUserApiKeyCreateRequest(string DisplayName);

public sealed record SecurityUserApiKeyRenameRequest(string DisplayName);

public sealed record SecurityRolePermissionOverrideRequest(string Permission, bool IsAllowed);

public sealed record SecurityRolePermissionOverridesRequest(
    IReadOnlyList<SecurityRolePermissionOverrideRequest>? Overrides);

public sealed record SecurityCustomRoleUpsertRequest(
    string Id,
    string Name,
    IReadOnlyList<string>? Permissions,
    bool? IsEnabled);

public sealed record SecurityUserUpsertRequest(
    string Id,
    string Username,
    string DisplayName,
    string? Password,
    IReadOnlyList<string>? RoleIds,
    bool? IsEnabled);
