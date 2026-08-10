import type { AppRoute } from "@/features/short-links/types";
import { APP_ROUTES } from "@/shared/constants/routes";
import { HTTP_STATUS } from "@/shared/constants/http";

export function parseRoute(pathname: string): AppRoute {
  if (pathname === APP_ROUTES.HOME) {
    return { kind: "home" };
  }

  if (pathname === APP_ROUTES.SHORT_LINKS) {
    return { kind: "admin" };
  }

  if (pathname === APP_ROUTES.ADMIN_DASHBOARD) {
    return { kind: "dashboard" };
  }

  if (pathname === APP_ROUTES.AUDIT_LOGS) {
    return { kind: "audit" };
  }

  if (pathname === APP_ROUTES.BULK_JOBS) {
    return { kind: "bulk-jobs" };
  }

  if (pathname === APP_ROUTES.ADMIN_SECURITY) {
    return { kind: "security", section: "users" };
  }

  const securityMatch = new RegExp(`^${APP_ROUTES.ADMIN_SECURITY}/(users|roles)$`).exec(pathname);
  if (securityMatch) {
    return { kind: "security", section: securityMatch[1] as "users" | "roles" };
  }

  if (pathname === APP_ROUTES.LOGIN) {
    return { kind: "login" };
  }

  if (pathname === APP_ROUTES.UNAUTHORIZED) {
    return { kind: "status", statusCode: HTTP_STATUS.UNAUTHORIZED };
  }

  if (pathname === APP_ROUTES.FORBIDDEN) {
    return { kind: "status", statusCode: HTTP_STATUS.FORBIDDEN };
  }

  if (pathname === APP_ROUTES.NOT_FOUND) {
    return { kind: "status", statusCode: HTTP_STATUS.NOT_FOUND };
  }

  const detailMatch = new RegExp(`^${APP_ROUTES.LINK_DETAILS}/([^/]+)$`).exec(pathname);
  if (detailMatch) {
    const code = decodeURIComponent(detailMatch[1] ?? "").trim();
    return code
      ? { kind: "detail", code }
      : { kind: "status", statusCode: HTTP_STATUS.NOT_FOUND };
  }

  return { kind: "status", statusCode: HTTP_STATUS.NOT_FOUND };
}
