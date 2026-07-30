namespace ShortenLink.Core.Domain;

public sealed class ShortLinkAuditEvent
{
    public ShortLinkAuditEvent(
        string actorId,
        string action,
        string targetId,
        string? ownerUserId,
        DateTimeOffset occurredAt,
        string outcome = ShortLinkAuditOutcomes.Succeeded,
        string? subjectUserId = null,
        string? detail = null,
        Guid? id = null,
        string targetType = ShortLinkAuditTargetTypes.ShortLink)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetType);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);

        Id = id ?? Guid.CreateVersion7();
        ActorId = actorId.Trim();
        Action = action.Trim();
        TargetType = targetType.Trim();
        TargetId = targetId.Trim();
        OwnerUserId = Normalize(ownerUserId);
        OccurredAt = occurredAt;
        Outcome = outcome.Trim();
        SubjectUserId = Normalize(subjectUserId);
        Detail = Normalize(detail);
    }

    public Guid Id { get; }

    public string ActorId { get; }

    public string Action { get; }

    public string TargetType { get; }

    public string TargetId { get; }

    public string? OwnerUserId { get; }

    public string Outcome { get; }

    public DateTimeOffset OccurredAt { get; }

    public string? SubjectUserId { get; }

    public string? Detail { get; }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public static class ShortLinkAuditActions
{
    public const string Created = "short_link.created";
    public const string Updated = "short_link.updated";
    public const string Activated = "short_link.activated";
    public const string Deactivated = "short_link.deactivated";
    public const string Deleted = "short_link.deleted";
    public const string ShareGranted = "short_link.share.granted";
    public const string ShareUpdated = "short_link.share.updated";
    public const string ShareRevoked = "short_link.share.revoked";
    public const string AuthenticationLogin = "authentication.login";
    public const string AuthenticationRefresh = "authentication.refresh";
    public const string UserApiKeyCreated = "user_api_key.created";
    public const string UserApiKeyRenamed = "user_api_key.renamed";
    public const string UserApiKeyDisabled = "user_api_key.disabled";
    public const string SecurityUserCreated = "security_user.created";
    public const string SecurityUserUpdated = "security_user.updated";
    public const string SecurityUserDisabled = "security_user.disabled";
    public const string SecurityRoleCreated = "security_role.created";
    public const string SecurityRoleUpdated = "security_role.updated";
    public const string SecurityRoleDeleted = "security_role.deleted";
    public const string SecurityRolePermissionsReplaced = "security_role.permissions_replaced";
    public const string SecurityAssignmentCreated = "security_assignment.created";
    public const string SecurityAssignmentUpdated = "security_assignment.updated";
    public const string SecurityAssignmentDisabled = "security_assignment.disabled";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(
        [
            Created,
            Updated,
            Activated,
            Deactivated,
            Deleted,
            ShareGranted,
            ShareUpdated,
            ShareRevoked,
            AuthenticationLogin,
            AuthenticationRefresh,
            UserApiKeyCreated,
            UserApiKeyRenamed,
            UserApiKeyDisabled,
            SecurityUserCreated,
            SecurityUserUpdated,
            SecurityUserDisabled,
            SecurityRoleCreated,
            SecurityRoleUpdated,
            SecurityRoleDeleted,
            SecurityRolePermissionsReplaced,
            SecurityAssignmentCreated,
            SecurityAssignmentUpdated,
            SecurityAssignmentDisabled
        ],
        StringComparer.Ordinal);
}

public static class ShortLinkAuditTargetTypes
{
    public const string ShortLink = "short_link";
    public const string Authentication = "authentication";
    public const string UserApiKey = "user_api_key";
    public const string SecurityUser = "security_user";
    public const string SecurityRole = "security_role";
    public const string SecurityAssignment = "security_assignment";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(
        [
            ShortLink,
            Authentication,
            UserApiKey,
            SecurityUser,
            SecurityRole,
            SecurityAssignment
        ],
        StringComparer.Ordinal);
}

public static class ShortLinkAuditOutcomes
{
    public const string Succeeded = "succeeded";
}
