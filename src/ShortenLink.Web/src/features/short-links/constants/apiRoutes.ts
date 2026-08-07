const API_ROOT = "/api";
const SECURITY_ROOT = `${API_ROOT}/security`;
const SHORT_LINK_ROOT = `${API_ROOT}/short-links`;

const pathSegment = (value: string) => encodeURIComponent(value);

export const SHORT_LINK_API_ROUTES = {
  LOGIN: `${SECURITY_ROOT}/login`,
  REFRESH: `${SECURITY_ROOT}/refresh`,
  CURRENT_USER: `${SECURITY_ROOT}/me`,
  AUDIT_LOGS: `${API_ROOT}/audit-logs`,
  AUDIT_LOG_ACTIONS: `${API_ROOT}/audit-logs/actions`,
  RATE_LIMITS: `${API_ROOT}/admin/rate-limits`,
  SHORT_LINKS: SHORT_LINK_ROOT,
  SHORT_LINK: (code: string) => `${SHORT_LINK_ROOT}/${pathSegment(code)}`,
  SHORT_LINK_ANALYTICS: (code: string) => `${SHORT_LINK_ROOT}/${pathSegment(code)}/analytics`,
  SHORT_LINK_DEACTIVATE: (code: string) => `${SHORT_LINK_ROOT}/${pathSegment(code)}/deactivate`,
  SHORT_LINK_ACTIVATE: (code: string) => `${SHORT_LINK_ROOT}/${pathSegment(code)}/activate`,
  SHORT_LINK_SHARES: (code: string) => `${SHORT_LINK_ROOT}/${pathSegment(code)}/shares`,
  SHORT_LINK_SHARE: (code: string, userId: string) =>
    `${SHORT_LINK_ROOT}/${pathSegment(code)}/shares/${pathSegment(userId)}`,
  SHORT_LINK_SHARING_MODE: (code: string) => `${SHORT_LINK_ROOT}/${pathSegment(code)}/sharing-mode`,
  SECURITY_ASSIGNMENTS: `${SECURITY_ROOT}/assignments`,
  SECURITY_ASSIGNMENT_DISABLE: (credentialKeyHash: string) =>
    `${SECURITY_ROOT}/assignments/${pathSegment(credentialKeyHash)}/disable`,
  SECURITY_ROLES: `${SECURITY_ROOT}/roles`,
  SECURITY_CUSTOM_ROLES: `${SECURITY_ROOT}/roles/custom`,
  SECURITY_CUSTOM_ROLE: (id: string) => `${SECURITY_ROOT}/roles/custom/${pathSegment(id)}`,
  SECURITY_PERMISSION_OVERRIDES: (roleId: string) =>
    `${SECURITY_ROOT}/roles/${pathSegment(roleId)}/permission-overrides`,
  SECURITY_USERS: `${SECURITY_ROOT}/users`,
  SECURITY_USER_DISABLE: (id: string) => `${SECURITY_ROOT}/users/${pathSegment(id)}/disable`,
  SECURITY_API_KEYS: `${SECURITY_ROOT}/api-keys`,
  SECURITY_API_KEY: (id: string) => `${SECURITY_ROOT}/api-keys/${pathSegment(id)}`,
  SECURITY_API_KEY_DISABLE: (id: string) => `${SECURITY_ROOT}/api-keys/${pathSegment(id)}/disable`
} as const;
