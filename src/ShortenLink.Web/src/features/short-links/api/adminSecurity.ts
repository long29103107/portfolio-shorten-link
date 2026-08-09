import type { SecurityCurrentUser } from "../types";
import { HTTP_HEADERS } from "@/shared/constants/http";
import { APP_EVENTS } from "@/shared/constants/events";

export const shortLinkPermissions = {
  read: "short_links.read",
  create: "short_links.create",
  update: "short_links.update",
  status: "short_links.status",
  delete: "short_links.delete",
  import: "short_links.import",
  analyticsRead: "analytics.read",
  auditLogsRead: "audit_logs.read"
} as const;

const allPermissions = Object.values(shortLinkPermissions);
const SESSION_STORAGE_KEYS = {
  ACCESS_TOKEN: "shortenLink.accessToken",
  REFRESH_TOKEN: "shortenLink.refreshToken",
  LEGACY_SESSION_TOKEN: "shortenLink.sessionToken",
  CURRENT_USER: "shortenLink.currentUser"
} as const;

const rolePermissionBundles: Record<string, readonly string[]> = {
  admin: allPermissions,
  user: [
    shortLinkPermissions.read,
    shortLinkPermissions.create,
    shortLinkPermissions.update,
    shortLinkPermissions.status,
    shortLinkPermissions.delete,
    shortLinkPermissions.import,
    shortLinkPermissions.analyticsRead,
    shortLinkPermissions.auditLogsRead
  ]
};

export type AdminPermissionState = {
  canCreate: boolean;
  canUpdate: boolean;
  canActivate: boolean;
  canDeactivate: boolean;
  canDelete: boolean;
  canReadAnalytics: boolean;
  canReadAuditLogs: boolean;
  canManageSecurityAssignments: boolean;
};

export function getStoredSessionToken(): string | null {
  return window.localStorage.getItem(SESSION_STORAGE_KEYS.ACCESS_TOKEN)
    ?? window.localStorage.getItem(SESSION_STORAGE_KEYS.LEGACY_SESSION_TOKEN);
}

export function getStoredRefreshToken(): string | null {
  return window.localStorage.getItem(SESSION_STORAGE_KEYS.REFRESH_TOKEN);
}

export function getStoredCurrentUser(): SecurityCurrentUser | null {
  const value = window.localStorage.getItem(SESSION_STORAGE_KEYS.CURRENT_USER);
  if (!value) {
    return null;
  }

  try {
    return JSON.parse(value) as SecurityCurrentUser;
  } catch {
    clearStoredSession();
    return null;
  }
}

export function storeSession(accessToken: string, refreshToken: string, user: SecurityCurrentUser): void {
  window.localStorage.setItem(SESSION_STORAGE_KEYS.ACCESS_TOKEN, accessToken);
  window.localStorage.setItem(SESSION_STORAGE_KEYS.REFRESH_TOKEN, refreshToken);
  window.localStorage.removeItem(SESSION_STORAGE_KEYS.LEGACY_SESSION_TOKEN);
  window.localStorage.setItem(SESSION_STORAGE_KEYS.CURRENT_USER, JSON.stringify(user));
  window.dispatchEvent(new Event(APP_EVENTS.AUTH_CHANGED));
}

export function clearStoredSession(): void {
  window.localStorage.removeItem(SESSION_STORAGE_KEYS.ACCESS_TOKEN);
  window.localStorage.removeItem(SESSION_STORAGE_KEYS.REFRESH_TOKEN);
  window.localStorage.removeItem(SESSION_STORAGE_KEYS.LEGACY_SESSION_TOKEN);
  window.localStorage.removeItem(SESSION_STORAGE_KEYS.CURRENT_USER);
  window.dispatchEvent(new Event(APP_EVENTS.AUTH_CHANGED));
}

export function getAdminApiKeyHeader(): Record<string, string> {
  const sessionToken = getStoredSessionToken();
  if (sessionToken) {
    return { [HTTP_HEADERS.AUTHORIZATION]: `Bearer ${sessionToken}` };
  }

  const apiKey = import.meta.env.VITE_SHORTENLINK_ADMIN_API_KEY?.trim();
  if (!apiKey) {
    return {};
  }

  const headerName =
    import.meta.env.VITE_SHORTENLINK_ADMIN_API_KEY_HEADER?.trim()
    || "X-ShortenLink-Api-Key";

  return { [headerName]: apiKey };
}

export function getAdminPermissionState(): AdminPermissionState {
  const permissions = getConfiguredPermissions();

  return {
    canCreate: permissions.has(shortLinkPermissions.create),
    canUpdate: permissions.has(shortLinkPermissions.update),
    canActivate: permissions.has(shortLinkPermissions.status),
    canDeactivate: permissions.has(shortLinkPermissions.status),
    canDelete: permissions.has(shortLinkPermissions.delete),
    canReadAnalytics: permissions.has(shortLinkPermissions.analyticsRead),
    canReadAuditLogs: permissions.has(shortLinkPermissions.auditLogsRead),
    canManageSecurityAssignments: getStoredCurrentUser()?.roles
      .some((role) => role.toLowerCase() === "admin") ?? false
  };
}

function getConfiguredPermissions(): Set<string> {
  const currentUser = getStoredCurrentUser();
  if (currentUser) {
    return new Set(currentUser.permissions);
  }

  const configuredPermissions = parseList(import.meta.env.VITE_SHORTENLINK_ADMIN_PERMISSIONS);
  const configuredRoles = parseList(import.meta.env.VITE_SHORTENLINK_ADMIN_ROLE);

  if (configuredPermissions.length === 0 && configuredRoles.length === 0) {
    return new Set(allPermissions);
  }

  const permissions = new Set(configuredPermissions);
  for (const role of configuredRoles) {
    const rolePermissions = rolePermissionBundles[role.toLowerCase()];
    rolePermissions?.forEach((permission) => permissions.add(permission));
  }

  return permissions;
}

function parseList(value: string | undefined): string[] {
  if (!value) {
    return [];
  }

  return value
    .split(",")
    .map((item) => item.trim())
    .filter(Boolean);
}
