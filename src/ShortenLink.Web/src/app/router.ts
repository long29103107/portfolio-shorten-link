import type { AppRoute } from "../features/short-links/types";

export function parseRoute(pathname: string): AppRoute {
  if (pathname === "/") {
    return { kind: "home" };
  }

  if (pathname === "/short-links") {
    return { kind: "admin" };
  }

  if (pathname === "/admin/dashboard") {
    return { kind: "dashboard" };
  }

  if (pathname === "/audit-logs") {
    return { kind: "audit" };
  }

  if (pathname === "/admin/security") {
    return { kind: "security", section: "users" };
  }

  const securityMatch = /^\/admin\/security\/(users|roles)$/.exec(pathname);
  if (securityMatch) {
    return { kind: "security", section: securityMatch[1] as "users" | "roles" };
  }

  if (pathname === "/login") {
    return { kind: "login" };
  }

  if (pathname === "/unauthorized") {
    return { kind: "status", statusCode: 401 };
  }

  if (pathname === "/forbidden") {
    return { kind: "status", statusCode: 403 };
  }

  if (pathname === "/not-found") {
    return { kind: "status", statusCode: 404 };
  }

  const detailMatch = /^\/links\/([^/]+)$/.exec(pathname);
  if (detailMatch) {
    const code = decodeURIComponent(detailMatch[1] ?? "").trim();
    return code ? { kind: "detail", code } : { kind: "status", statusCode: 404 };
  }

  return { kind: "status", statusCode: 404 };
}
