const configuredApiBaseUrl = import.meta.env.VITE_SHORTENLINK_API_BASE_URL?.trim().replace(/\/+$/, "");
const API_ROOT = configuredApiBaseUrl ? `${configuredApiBaseUrl}/api` : "/api";
const SECURITY_ROOT = `${API_ROOT}/security`;
const SHORT_LINK_ROOT = `${API_ROOT}/short-links`;

const pathSegment = (value: string) => encodeURIComponent(value);

const authRoutes = {
  LOGIN: `${SECURITY_ROOT}/login`,
  REFRESH: `${SECURITY_ROOT}/refresh`,
  CURRENT_USER: `${SECURITY_ROOT}/me`
} as const;

const auditRoutes = {
  LOGS: `${API_ROOT}/audit-logs`,
  ACTIONS: `${API_ROOT}/audit-logs/actions`
} as const;

const adminRoutes = {
  RATE_LIMITS: `${API_ROOT}/admin/rate-limits`
} as const;

const shortLinkRoutes = {
  ROOT: SHORT_LINK_ROOT,
  BULK: `${SHORT_LINK_ROOT}/bulk`,
  BULK_JOBS: `${SHORT_LINK_ROOT}/bulk/jobs`,
  BULK_JOB: (jobId: string) => `${SHORT_LINK_ROOT}/bulk/jobs/${pathSegment(jobId)}`,
  CANCEL_BULK_JOB: (jobId: string) => `${SHORT_LINK_ROOT}/bulk/jobs/${pathSegment(jobId)}`,
  BY_CODE: (code: string) => `${SHORT_LINK_ROOT}/${pathSegment(code)}`,
  ANALYTICS: (code: string) => `${SHORT_LINK_ROOT}/${pathSegment(code)}/analytics`,
  DEACTIVATE: (code: string) => `${SHORT_LINK_ROOT}/${pathSegment(code)}/deactivate`,
  ACTIVATE: (code: string) => `${SHORT_LINK_ROOT}/${pathSegment(code)}/activate`,
  SHARES: (code: string) => `${SHORT_LINK_ROOT}/${pathSegment(code)}/shares`,
  SHARE: (code: string, userId: string) =>
    `${SHORT_LINK_ROOT}/${pathSegment(code)}/shares/${pathSegment(userId)}`,
  SHARING_MODE: (code: string) => `${SHORT_LINK_ROOT}/${pathSegment(code)}/sharing-mode`
} as const;

const securityRoutes = {
  ASSIGNMENTS: `${SECURITY_ROOT}/assignments`,
  ASSIGNMENT_DISABLE: (credentialKeyHash: string) =>
    `${SECURITY_ROOT}/assignments/${pathSegment(credentialKeyHash)}/disable`,
  ROLES: `${SECURITY_ROOT}/roles`,
  CUSTOM_ROLES: `${SECURITY_ROOT}/roles/custom`,
  CUSTOM_ROLE: (id: string) => `${SECURITY_ROOT}/roles/custom/${pathSegment(id)}`,
  PERMISSION_OVERRIDES: (roleId: string) =>
    `${SECURITY_ROOT}/roles/${pathSegment(roleId)}/permission-overrides`,
  USERS: `${SECURITY_ROOT}/users`,
  USER_DISABLE: (id: string) => `${SECURITY_ROOT}/users/${pathSegment(id)}/disable`,
  API_KEYS: `${SECURITY_ROOT}/api-keys`,
  API_KEY: (id: string) => `${SECURITY_ROOT}/api-keys/${pathSegment(id)}`,
  API_KEY_DISABLE: (id: string) => `${SECURITY_ROOT}/api-keys/${pathSegment(id)}/disable`
} as const;

export const SHORT_LINK_API_ROUTES = {
  AUTH: authRoutes,
  AUDIT: auditRoutes,
  ADMIN: adminRoutes,
  SHORT_LINK: shortLinkRoutes,
  SECURITY: securityRoutes
} as const;
