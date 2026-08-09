import { apiClient } from "./apiClient";
import { appendQueryExpression } from "@/shared/queryExpression";
import { buildShortLinkFilterExpression, buildShortLinkSortExpression } from "../domain/queryExpression";
import type {
  AuditLogPage,
  AuditLogActions,
  AuditLogQuery,
  CreateShortLinkRequest,
  CreatedShortLink,
  DeactivatedShortLink,
  DeletedShortLink,
  SecurityAssignment,
  SecurityAssignmentDisabled,
  SecurityAssignmentsList,
  SecurityAssignmentUpsertRequest,
  SecurityCustomRoleUpsertRequest,
  SecurityCurrentUser,
  SecurityLoginResponse,
  SecurityRole,
  SecurityRolePermissionOverridesRequest,
  SecurityRoleDeleted,
  SecurityRolesList,
  SecurityUser,
  SecurityUserApiKey,
  SecurityUserApiKeyCreated,
  SecurityUserApiKeyDisabled,
  SecurityUserApiKeysList,
  SecurityUserDisabled,
  SecurityUsersList,
  SecurityUserUpsertRequest,
  ShortLinkAnalytics,
  ShortLinkAdminItem,
  ShortLinkAdminPageResult,
  ShortLinkDiscoveryQuery,
  ShortLinkDetails,
  RateLimitActivity,
  ShortLinkShare,
  ShortLinkSharesList,
  ShortLinkSharingMode,
  UpdateShortLinkRequest
} from "../types";
import { buildAuditLogUrl } from "../domain/auditDiscovery";
import { SHORT_LINK_API_ROUTES } from "../constants/apiRoutes";
import { SHORT_LINK_DISCOVERY_DEFAULTS } from "../constants/defaults";

export async function loginSecurityUser(
  email: string,
  password: string
): Promise<SecurityLoginResponse> {
  return apiClient.post<SecurityLoginResponse>(SHORT_LINK_API_ROUTES.LOGIN, { email, password }, {
    suppressAuthRedirect: true,
  });
}

export async function getCurrentSecurityUser(): Promise<SecurityCurrentUser> {
  return apiClient.get<SecurityCurrentUser>(SHORT_LINK_API_ROUTES.CURRENT_USER);
}

export async function listAuditLogEvents(
  query: AuditLogQuery = {},
  signal?: AbortSignal
): Promise<AuditLogPage> {
  return apiClient.get<AuditLogPage>(
    buildAuditLogUrl(query),
    signal ? { signal } : undefined
  );
}

export async function listAuditLogActions(signal?: AbortSignal): Promise<AuditLogActions> {
  return apiClient.get<AuditLogActions>(
    SHORT_LINK_API_ROUTES.AUDIT_LOG_ACTIONS,
    signal ? { signal } : undefined
  );
}

export async function getRateLimitActivity(signal?: AbortSignal): Promise<RateLimitActivity> {
  return apiClient.get<RateLimitActivity>(
    SHORT_LINK_API_ROUTES.RATE_LIMITS,
    signal ? { signal } : undefined
  );
}

export async function createShortLink(
  request: CreateShortLinkRequest
): Promise<CreatedShortLink> {
  return apiClient.post<CreatedShortLink>(SHORT_LINK_API_ROUTES.SHORT_LINKS, request);
}

export async function getShortLinkDetails(code: string, signal?: AbortSignal): Promise<ShortLinkDetails> {
  return apiClient.get<ShortLinkDetails>(
    SHORT_LINK_API_ROUTES.SHORT_LINK(code),
    signal ? { signal } : undefined
  );
}

export async function getShortLinkAnalytics(code: string, signal?: AbortSignal): Promise<ShortLinkAnalytics> {
  return apiClient.get<ShortLinkAnalytics>(
    SHORT_LINK_API_ROUTES.SHORT_LINK_ANALYTICS(code),
    signal ? { signal } : undefined
  );
}

export async function listShortLinks(
  limit = SHORT_LINK_DISCOVERY_DEFAULTS.LIMIT,
  page = SHORT_LINK_DISCOVERY_DEFAULTS.PAGE,
  discovery?: ShortLinkDiscoveryQuery,
  signal?: AbortSignal
): Promise<ShortLinkAdminPageResult> {
  const query = Object.fromEntries(buildShortLinkQueryParams(limit, page, discovery));
  return apiClient.query<ShortLinkAdminPageResult>(
    SHORT_LINK_API_ROUTES.SHORT_LINKS,
    query,
    signal ? { signal } : undefined
  );
}

export function buildShortLinkQueryParams(
  limit = SHORT_LINK_DISCOVERY_DEFAULTS.LIMIT,
  page = SHORT_LINK_DISCOVERY_DEFAULTS.PAGE,
  discovery?: ShortLinkDiscoveryQuery
) {
  const params = new URLSearchParams({
    limit: String(limit),
    page: String(page)
  });

  if (discovery) {
    appendQueryExpression(params, {
      filter: buildShortLinkFilterExpression(discovery),
      sort: buildShortLinkSortExpression(discovery)
    });
    const folder = discovery.folder?.trim() ?? "";
    const tag = discovery.tag?.trim() ?? "";
    if (folder) {
      params.set("folder", folder);
    }
    if (tag) {
      params.set("tag", tag);
    }
  }

  return params;
}

export function buildShortLinkListUrl(
  limit = SHORT_LINK_DISCOVERY_DEFAULTS.LIMIT,
  page = SHORT_LINK_DISCOVERY_DEFAULTS.PAGE,
  discovery?: ShortLinkDiscoveryQuery
) {
  return `${SHORT_LINK_API_ROUTES.SHORT_LINKS}?${buildShortLinkQueryParams(limit, page, discovery).toString()}`;
}

export async function deactivateShortLink(code: string): Promise<DeactivatedShortLink> {
  return apiClient.post<DeactivatedShortLink>(SHORT_LINK_API_ROUTES.SHORT_LINK_DEACTIVATE(code));
}

export async function activateShortLink(code: string): Promise<DeactivatedShortLink> {
  return apiClient.post<DeactivatedShortLink>(SHORT_LINK_API_ROUTES.SHORT_LINK_ACTIVATE(code));
}

export async function updateShortLink(
  code: string,
  request: UpdateShortLinkRequest
): Promise<ShortLinkAdminItem> {
  return apiClient.put<ShortLinkAdminItem>(SHORT_LINK_API_ROUTES.SHORT_LINK(code), request);
}

export async function deleteShortLink(code: string): Promise<DeletedShortLink> {
  return apiClient.delete<DeletedShortLink>(SHORT_LINK_API_ROUTES.SHORT_LINK(code));
}

export async function listSecurityAssignments(signal?: AbortSignal): Promise<SecurityAssignmentsList> {
  return apiClient.get<SecurityAssignmentsList>(
    SHORT_LINK_API_ROUTES.SECURITY_ASSIGNMENTS,
    signal ? { signal } : undefined
  );
}

export async function upsertSecurityAssignment(
  request: SecurityAssignmentUpsertRequest
): Promise<SecurityAssignment> {
  return apiClient.put<SecurityAssignment>(SHORT_LINK_API_ROUTES.SECURITY_ASSIGNMENTS, request);
}

export async function disableSecurityAssignment(
  credentialKeyHash: string
): Promise<SecurityAssignmentDisabled> {
  return apiClient.post<SecurityAssignmentDisabled>(
    SHORT_LINK_API_ROUTES.SECURITY_ASSIGNMENT_DISABLE(credentialKeyHash),
  );
}

export async function listSecurityRoles(signal?: AbortSignal): Promise<SecurityRolesList> {
  return apiClient.get<SecurityRolesList>(
    SHORT_LINK_API_ROUTES.SECURITY_ROLES,
    signal ? { signal } : undefined
  );
}

export async function upsertCustomSecurityRole(
  request: SecurityCustomRoleUpsertRequest
): Promise<SecurityRole> {
  return apiClient.put<SecurityRole>(SHORT_LINK_API_ROUTES.SECURITY_CUSTOM_ROLES, request);
}

export async function deleteCustomSecurityRole(id: string): Promise<SecurityRoleDeleted> {
  return apiClient.delete<SecurityRoleDeleted>(
    SHORT_LINK_API_ROUTES.SECURITY_CUSTOM_ROLE(id),
  );
}

export async function listShortLinkShares(
  code: string,
  signal?: AbortSignal
): Promise<ShortLinkSharesList> {
  return apiClient.get<ShortLinkSharesList>(
    SHORT_LINK_API_ROUTES.SHORT_LINK_SHARES(code),
    signal ? { signal } : undefined
  );
}

export async function setShortLinkSharingMode(
  code: string,
  mode: ShortLinkSharingMode
): Promise<ShortLinkSharingMode> {
  return apiClient.put<ShortLinkSharingMode>(
    SHORT_LINK_API_ROUTES.SHORT_LINK_SHARING_MODE(code),
    { mode }
  );
}

export async function upsertShortLinkShare(
  code: string,
  username: string,
  access: "View" | "Edit"
): Promise<ShortLinkShare> {
  return apiClient.put<ShortLinkShare>(
    SHORT_LINK_API_ROUTES.SHORT_LINK_SHARES(code),
    { username, access }
  );
}

export async function deleteShortLinkShare(code: string, userId: string): Promise<void> {
  await apiClient.delete<void>(
    SHORT_LINK_API_ROUTES.SHORT_LINK_SHARE(code, userId),
  );
}

export async function replaceSecurityRolePermissionOverrides(
  roleId: string,
  request: SecurityRolePermissionOverridesRequest
): Promise<SecurityRole> {
  return apiClient.put<SecurityRole>(
    SHORT_LINK_API_ROUTES.SECURITY_PERMISSION_OVERRIDES(roleId),
    request
  );
}

export async function listSecurityUsers(signal?: AbortSignal): Promise<SecurityUsersList> {
  return apiClient.get<SecurityUsersList>(
    SHORT_LINK_API_ROUTES.SECURITY_USERS,
    signal ? { signal } : undefined
  );
}

export async function upsertSecurityUser(request: SecurityUserUpsertRequest): Promise<SecurityUser> {
  return apiClient.put<SecurityUser>(SHORT_LINK_API_ROUTES.SECURITY_USERS, request);
}

export async function disableSecurityUser(id: string): Promise<SecurityUserDisabled> {
  return apiClient.post<SecurityUserDisabled>(SHORT_LINK_API_ROUTES.SECURITY_USER_DISABLE(id));
}

export async function listUserApiKeys(): Promise<SecurityUserApiKeysList> {
  return apiClient.get<SecurityUserApiKeysList>(SHORT_LINK_API_ROUTES.SECURITY_API_KEYS);
}

export async function createUserApiKey(displayName: string): Promise<SecurityUserApiKeyCreated> {
  return apiClient.post<SecurityUserApiKeyCreated>(SHORT_LINK_API_ROUTES.SECURITY_API_KEYS, { displayName });
}

export async function renameUserApiKey(id: string, displayName: string): Promise<SecurityUserApiKey> {
  return apiClient.put<SecurityUserApiKey>(SHORT_LINK_API_ROUTES.SECURITY_API_KEY(id), { displayName });
}

export async function disableUserApiKey(id: string): Promise<SecurityUserApiKeyDisabled> {
  return apiClient.post<SecurityUserApiKeyDisabled>(
    SHORT_LINK_API_ROUTES.SECURITY_API_KEY_DISABLE(id),
  );
}
