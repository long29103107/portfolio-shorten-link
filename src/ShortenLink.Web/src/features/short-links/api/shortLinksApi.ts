import { fetchJson } from "./http";
import { appendQueryExpression } from "../../../shared/queryExpression";
import { buildShortLinkFilterExpression, buildShortLinkSortExpression } from "../queryExpression";
import type {
  AuditLogPage,
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
  UpdateShortLinkRequest
} from "../types";
import { buildAuditLogUrl } from "../auditDiscovery";

export async function loginSecurityUser(
  email: string,
  password: string
): Promise<SecurityLoginResponse> {
  return fetchJson<SecurityLoginResponse>("/api/security/login", {
    method: "POST",
    suppressAuthRedirect: true,
    body: JSON.stringify({ email, password })
  });
}

export async function getCurrentSecurityUser(): Promise<SecurityCurrentUser> {
  return fetchJson<SecurityCurrentUser>("/api/security/me");
}

export async function listAuditLogEvents(query: AuditLogQuery = {}): Promise<AuditLogPage> {
  return fetchJson<AuditLogPage>(buildAuditLogUrl(query));
}

export async function getRateLimitActivity(): Promise<RateLimitActivity> {
  return fetchJson<RateLimitActivity>("/api/admin/rate-limits");
}

export async function createShortLink(
  request: CreateShortLinkRequest
): Promise<CreatedShortLink> {
  return fetchJson<CreatedShortLink>("/api/short-links", {
    method: "POST",
    body: JSON.stringify(request)
  });
}

export async function getShortLinkDetails(code: string): Promise<ShortLinkDetails> {
  return fetchJson<ShortLinkDetails>(`/api/short-links/${encodeURIComponent(code)}`);
}

export async function getShortLinkAnalytics(code: string): Promise<ShortLinkAnalytics> {
  return fetchJson<ShortLinkAnalytics>(`/api/short-links/${encodeURIComponent(code)}/analytics`);
}

export async function listShortLinks(
  limit = 25,
  page = 1,
  discovery?: ShortLinkDiscoveryQuery
): Promise<ShortLinkAdminPageResult> {
  return fetchJson<ShortLinkAdminPageResult>(buildShortLinkListUrl(limit, page, discovery));
}

export function buildShortLinkListUrl(
  limit = 25,
  page = 1,
  discovery?: ShortLinkDiscoveryQuery
) {
  const params = new URLSearchParams({
    limit: String(limit),
    page: String(page)
  });

  if (discovery) {
    const search = discovery.search.trim();
    if (search) {
      params.set("search", search);
    }
    params.set("status", discovery.status);
    params.set("sortBy", discovery.sortBy);
    params.set("sortDirection", discovery.sortDirection);
    appendQueryExpression(params, {
      filter: buildShortLinkFilterExpression(discovery),
      sort: buildShortLinkSortExpression(discovery)
    });
  }

  return `/api/short-links?${params.toString()}`;
}

export async function deactivateShortLink(code: string): Promise<DeactivatedShortLink> {
  return fetchJson<DeactivatedShortLink>(`/api/short-links/${encodeURIComponent(code)}/deactivate`, {
    method: "POST"
  });
}

export async function activateShortLink(code: string): Promise<DeactivatedShortLink> {
  return fetchJson<DeactivatedShortLink>(`/api/short-links/${encodeURIComponent(code)}/activate`, {
    method: "POST"
  });
}

export async function updateShortLink(
  code: string,
  request: UpdateShortLinkRequest
): Promise<ShortLinkAdminItem> {
  return fetchJson<ShortLinkAdminItem>(`/api/short-links/${encodeURIComponent(code)}`, {
    method: "PUT",
    body: JSON.stringify(request)
  });
}

export async function deleteShortLink(code: string): Promise<DeletedShortLink> {
  return fetchJson<DeletedShortLink>(`/api/short-links/${encodeURIComponent(code)}`, {
    method: "DELETE"
  });
}

export async function listSecurityAssignments(): Promise<SecurityAssignmentsList> {
  return fetchJson<SecurityAssignmentsList>("/api/security/assignments");
}

export async function upsertSecurityAssignment(
  request: SecurityAssignmentUpsertRequest
): Promise<SecurityAssignment> {
  return fetchJson<SecurityAssignment>("/api/security/assignments", {
    method: "PUT",
    body: JSON.stringify(request)
  });
}

export async function disableSecurityAssignment(
  credentialKeyHash: string
): Promise<SecurityAssignmentDisabled> {
  return fetchJson<SecurityAssignmentDisabled>(
    `/api/security/assignments/${encodeURIComponent(credentialKeyHash)}/disable`,
    { method: "POST" }
  );
}

export async function listSecurityRoles(): Promise<SecurityRolesList> {
  return fetchJson<SecurityRolesList>("/api/security/roles");
}

export async function upsertCustomSecurityRole(
  request: SecurityCustomRoleUpsertRequest
): Promise<SecurityRole> {
  return fetchJson<SecurityRole>("/api/security/roles/custom", {
    method: "PUT",
    body: JSON.stringify(request)
  });
}

export async function deleteCustomSecurityRole(id: string): Promise<SecurityRoleDeleted> {
  return fetchJson<SecurityRoleDeleted>(
    `/api/security/roles/custom/${encodeURIComponent(id)}`,
    { method: "DELETE" }
  );
}

export async function listShortLinkShares(code: string): Promise<ShortLinkSharesList> {
  return fetchJson<ShortLinkSharesList>(
    `/api/short-links/${encodeURIComponent(code)}/shares`
  );
}

export async function upsertShortLinkShare(
  code: string,
  username: string,
  access: "View" | "Edit"
): Promise<ShortLinkShare> {
  return fetchJson<ShortLinkShare>(
    `/api/short-links/${encodeURIComponent(code)}/shares`,
    {
      method: "PUT",
      body: JSON.stringify({ username, access })
    }
  );
}

export async function deleteShortLinkShare(code: string, userId: string): Promise<void> {
  await fetchJson<void>(
    `/api/short-links/${encodeURIComponent(code)}/shares/${encodeURIComponent(userId)}`,
    { method: "DELETE" }
  );
}

export async function replaceSecurityRolePermissionOverrides(
  roleId: string,
  request: SecurityRolePermissionOverridesRequest
): Promise<SecurityRole> {
  return fetchJson<SecurityRole>(
    `/api/security/roles/${encodeURIComponent(roleId)}/permission-overrides`,
    {
      method: "PUT",
      body: JSON.stringify(request)
    }
  );
}

export async function listSecurityUsers(): Promise<SecurityUsersList> {
  return fetchJson<SecurityUsersList>("/api/security/users");
}

export async function upsertSecurityUser(request: SecurityUserUpsertRequest): Promise<SecurityUser> {
  return fetchJson<SecurityUser>("/api/security/users", {
    method: "PUT",
    body: JSON.stringify(request)
  });
}

export async function disableSecurityUser(id: string): Promise<SecurityUserDisabled> {
  return fetchJson<SecurityUserDisabled>(`/api/security/users/${encodeURIComponent(id)}/disable`, {
    method: "POST"
  });
}

export async function listUserApiKeys(): Promise<SecurityUserApiKeysList> {
  return fetchJson<SecurityUserApiKeysList>("/api/security/api-keys");
}

export async function createUserApiKey(displayName: string): Promise<SecurityUserApiKeyCreated> {
  return fetchJson<SecurityUserApiKeyCreated>("/api/security/api-keys", {
    method: "POST",
    body: JSON.stringify({ displayName })
  });
}

export async function renameUserApiKey(id: string, displayName: string): Promise<SecurityUserApiKey> {
  return fetchJson<SecurityUserApiKey>(`/api/security/api-keys/${encodeURIComponent(id)}`, {
    method: "PUT",
    body: JSON.stringify({ displayName })
  });
}

export async function disableUserApiKey(id: string): Promise<SecurityUserApiKeyDisabled> {
  return fetchJson<SecurityUserApiKeyDisabled>(
    `/api/security/api-keys/${encodeURIComponent(id)}/disable`,
    { method: "POST" }
  );
}
