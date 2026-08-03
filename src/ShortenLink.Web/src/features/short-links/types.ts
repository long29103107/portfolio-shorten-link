export type AppRoute =
  | { kind: "home" }
  | { kind: "admin" }
  | { kind: "dashboard" }
  | { kind: "audit" }
  | { kind: "security"; section: SecuritySection }
  | { kind: "login" }
  | { kind: "detail"; code: string }
  | { kind: "status"; statusCode: HttpStatusCode };

export type HttpStatusCode = 401 | 403 | 404;

export type SecuritySection = "users" | "roles";

export type ShortLinkFormInput = {
  originalUrl: string;
  expiredAtLocal: string;
};

export type CreateShortLinkRequest = {
  originalUrl: string;
  expiredAtUtc: string;
};

export type UpdateShortLinkRequest = {
  originalUrl: string;
  expiredAtUtc: string;
};

export type CreatedShortLink = {
  code: string;
  shortUrl: string;
  originalUrl: string;
  createdAtUtc: string;
};

export type ShortLinkDetails = {
  code: string;
  originalUrl: string;
  createdAtUtc: string;
  expiredAtUtc: string | null;
  isActive: boolean;
};

export type ShortLinkAdminItem = {
  code: string;
  shortUrl: string;
  originalUrl: string;
  createdAtUtc: string;
  expiredAtUtc: string | null;
  isActive: boolean;
  createdByUserId: string | null;
  createdByDisplayName: string | null;
  createdByUsername: string | null;
  accessLevel: "Admin" | "Owner" | "Edit" | "View" | "None" | null;
};

export type ShortLinkShare = {
  userId: string;
  username: string | null;
  displayName: string | null;
  access: "View" | "Edit";
  createdByUserId: string;
  createdAtUtc: string;
};

export type ShortLinkSharingMode = "Public" | "AllowList";

export type ShortLinkSharesList = {
  mode: ShortLinkSharingMode;
  items: ShortLinkShare[];
};

export type ShortLinkAdminPageResult = {
  items: ShortLinkAdminItem[];
  nextCursor: string | null;
  totalCount: number | null;
  page: number | null;
  pageSize: number | null;
  totalPages: number | null;
};

export type ShortLinkAnalytics = {
  code: string;
  clickCount: number;
  lastClickedAtUtc: string | null;
  recentClicks: ShortLinkClickActivity[];
};

export type ShortLinkClickActivity = {
  clickedAtUtc: string;
  remoteIpAddress: string | null;
  userAgent: string | null;
  referrer: string | null;
};

export type AuditLogEvent = {
  id: string;
  actorId: string;
  action: string;
  targetType: string;
  targetId: string;
  ownerUserId: string | null;
  outcome: string;
  occurredAtUtc: string;
  subjectUserId: string | null;
  detail: string | null;
};

export type AuditLogPage = {
  items: AuditLogEvent[];
  nextCursor: string | null;
};

export type AuditLogActions = {
  items: string[];
};

export type AuditLogFilters = {
  action: string;
  targetId: string;
  actorId: string;
  from: string;
  to: string;
};

export type AuditLogQuery = {
  limit?: number;
  cursor?: string | null;
  filters?: AuditLogFilters;
};

export type RateLimitPolicyActivity = {
  permitLimit: number;
  windowSeconds: number;
  queueLimit: number;
  rejectedCount: number;
};

export type RateLimitRejection = {
  policy: string;
  occurredAtUtc: string;
};

export type RateLimitActivity = {
  enabled: boolean;
  create: RateLimitPolicyActivity;
  redirect: RateLimitPolicyActivity;
  recentRejections: RateLimitRejection[];
};

export type DeactivatedShortLink = {
  code: string;
  isActive: boolean;
};

export type DeletedShortLink = {
  code: string;
};

export type SecurityAssignment = {
  credentialKeyHash: string;
  name: string;
  roles: string[];
  permissions: string[];
  isEnabled: boolean;
  createdAtUtc: string;
};

export type SecurityAssignmentsList = {
  items: SecurityAssignment[];
};

export type SecurityAssignmentUpsertRequest = {
  name: string;
  credentialKey: string;
  roles: string[];
  permissions: string[];
  isEnabled: boolean;
};

export type SecurityAssignmentDisabled = {
  credentialKeyHash: string;
  isEnabled: boolean;
};

export type ShortLinkStatusFilter = "all" | "active" | "inactive" | "expired" | "expiring-soon";

export type ShortLinkSortField = "created" | "expiry" | "destination" | "code" | "status";

export type ShortLinkSortDirection = "asc" | "desc";

export type ShortLinkDiscoveryQuery = {
  search: string;
  status: ShortLinkStatusFilter;
  sortBy: ShortLinkSortField;
  sortDirection: ShortLinkSortDirection;
};

export type SecurityCurrentUser = {
  userId: string;
  username: string;
  displayName: string;
  roles: string[];
  permissions: string[];
  issuedAtUtc: string;
};

export type SecurityLoginResponse = {
  token: string;
  accessToken: string;
  refreshToken: string;
  user: SecurityCurrentUser;
};

export type SecurityRole = {
  id: string;
  name: string;
  permissions: string[];
  defaultPermissions: string[];
  permissionOverrides: SecurityRolePermissionOverride[];
  isSystem: boolean;
  isEnabled: boolean;
  canDelete: boolean;
  createdAtUtc: string | null;
};

export type SecurityRolePermissionOverride = {
  permission: string;
  isAllowed: boolean;
};

export type SecurityRolesList = {
  systemRoles: SecurityRole[];
  customRoles: SecurityRole[];
};

export type SecurityCustomRoleUpsertRequest = {
  id: string;
  name: string;
  permissions: string[];
  isEnabled: boolean;
};

export type SecurityRoleDeleted = {
  id: string;
};

export type SecurityRolePermissionOverridesRequest = {
  overrides: SecurityRolePermissionOverride[];
};

export type SecurityUser = {
  id: string;
  username: string;
  displayName: string;
  roleIds: string[];
  isEnabled: boolean;
  isHidden: boolean;
  isBootstrap: boolean;
  createdAtUtc: string;
};

export type SecurityUsersList = {
  items: SecurityUser[];
};

export type SecurityUserUpsertRequest = {
  id: string;
  username: string;
  displayName: string;
  password: string | null;
  roleIds: string[];
  isEnabled: boolean;
};

export type SecurityUserDisabled = {
  id: string;
  isEnabled: boolean;
};

export type SecurityUserApiKey = {
  id: string;
  displayName: string;
  isEnabled: boolean;
  createdAtUtc: string;
};

export type SecurityUserApiKeysList = {
  items: SecurityUserApiKey[];
};

export type SecurityUserApiKeyCreated = {
  apiKey: SecurityUserApiKey;
  rawApiKey: string;
};

export type SecurityUserApiKeyDisabled = {
  id: string;
  isEnabled: boolean;
};

export type ApiErrorPayload = {
  errorCode: string;
  message: string;
  fieldErrors?: Record<string, string>;
};

export function formatDateTime(value: string | null): string {
  if (!value) {
    return "No expiry";
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime())
    ? value
    : new Intl.DateTimeFormat(undefined, {
        dateStyle: "medium",
        timeStyle: "short"
      }).format(date);
}

export function toFriendlyErrorMessage(errorCode: string, fallbackMessage: string): string {
  switch (errorCode) {
    case "invalid_code":
      return "Enter a valid short-link code.";
    case "invalid_expiration":
      return "Expiry needs to be in the future.";
    case "invalid_url":
      return "Paste a full http:// or https:// URL.";
    case "inactive":
      return "This link has already been deactivated.";
    case "expired":
      return "This link has expired.";
    case "not_found":
      return "We could not find that short link.";
    case "invalid_role":
      return "Choose only built-in system roles.";
    case "invalid_permission":
      return "Choose only supported permissions.";
    case "invalid_security_assignment":
      return "Complete the security assignment fields.";
    case "invalid_credential_hash":
      return "The selected credential hash is invalid.";
    case "invalid_login":
      return "Username or password is invalid.";
    case "invalid_api_key":
      return "Complete the API key fields.";
    case "invalid_security_role":
      return "Complete the custom role fields.";
    case "invalid_security_user":
      return "Complete the user fields.";
    case "system_role_immutable":
      return "System roles cannot be changed.";
    case "role_in_use":
      return fallbackMessage;
    case "bootstrap_user_immutable":
      return "The bootstrap admin user cannot be changed here.";
    default:
      return fallbackMessage;
  }
}
