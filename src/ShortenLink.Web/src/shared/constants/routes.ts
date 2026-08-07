export const APP_ROUTES = {
  HOME: "/",
  LOGIN: "/login",
  UNAUTHORIZED: "/unauthorized",
  FORBIDDEN: "/forbidden",
  NOT_FOUND: "/not-found",
  SHORT_LINKS: "/short-links",
  AUDIT_LOGS: "/audit-logs",
  ADMIN_DASHBOARD: "/admin/dashboard",
  ADMIN_SECURITY: "/admin/security",
  LINK_DETAILS: "/links"
} as const;

export function buildSecurityRoute(section: "users" | "roles"): string {
  return `${APP_ROUTES.ADMIN_SECURITY}/${section}`;
}

export function buildShortLinkDetailRoute(code: string): string {
  return `${APP_ROUTES.LINK_DETAILS}/${encodeURIComponent(code)}`;
}
