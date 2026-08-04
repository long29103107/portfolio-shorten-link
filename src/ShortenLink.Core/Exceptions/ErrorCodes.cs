using ShortenLink.Core.Services;

namespace ShortenLink.Core.Exceptions;

public static class ErrorCodes
{
    public const string Unauthorized = "unauthorized";
    public const string Forbidden = "forbidden";
    public const string InternalError = "internal_error";
    public const string UnknownError = "unknown_error";
    public const string InvalidRequest = "invalid_request";
    public const string NotFound = ShortLinkErrorCodes.NotFound;
    public const string InvalidLogin = "invalid_login";
    public const string InvalidApiKey = "invalid_api_key";
    public const string InvalidCredentialHash = "invalid_credential_hash";
    public const string InvalidCursor = "invalid_cursor";
    public const string InvalidFilter = "invalid_filter";
    public const string InvalidSort = "invalid_sort";
    public const string InvalidSortDirection = "invalid_sort_direction";
    public const string InvalidSecurityUser = "invalid_security_user";
    public const string InvalidSecurityRole = "invalid_security_role";
    public const string InvalidSecurityAssignment = "invalid_security_assignment";
    public const string InvalidRole = "invalid_role";
    public const string InvalidPermission = "invalid_permission";
    public const string DuplicatePermission = "duplicate_permission";
    public const string SystemRoleImmutable = "system_role_immutable";
    public const string BootstrapUserImmutable = "bootstrap_user_immutable";
    public const string RoleInUse = "role_in_use";
    public const string InvalidShare = "invalid_share";
    public const string InvalidShareUser = "invalid_share_user";
    public const string DuplicateAlias = "duplicate_alias";
    public const string CreateFailed = "create_failed";
    public const string OperationFailed = "operation_failed";
}
