import { describe, expect, test } from "bun:test";
import type { AppRoute } from "../src/features/short-links/types";
import { APP_ROUTES } from "../src/shared/constants/routes";
import { getCurrentRoutePath, resolveNavigationPath } from "../src/app/hooks/useAppNavigation";

describe("application navigation", () => {
  test("maps protected route state back to its canonical path", () => {
    const cases: Array<[AppRoute, string]> = [
      [{ kind: "admin" }, APP_ROUTES.SHORT_LINKS],
      [{ kind: "dashboard" }, APP_ROUTES.ADMIN_DASHBOARD],
      [{ kind: "audit" }, APP_ROUTES.AUDIT_LOGS],
      [{ kind: "security", section: "roles" }, `${APP_ROUTES.ADMIN_SECURITY}/roles`]
    ];

    cases.forEach(([route, expectedPath]) => {
      expect(getCurrentRoutePath(route, "/fallback")).toBe(expectedPath);
    });
  });

  test("redirects unauthenticated navigation to login", () => {
    expect(resolveNavigationPath(APP_ROUTES.HOME, false)).toBe(APP_ROUTES.LOGIN);
    expect(resolveNavigationPath(APP_ROUTES.SHORT_LINKS, false)).toBe(APP_ROUTES.LOGIN);
    expect(resolveNavigationPath(APP_ROUTES.LOGIN, false)).toBe(APP_ROUTES.LOGIN);
  });

  test("preserves requested paths for an authenticated session", () => {
    expect(resolveNavigationPath(APP_ROUTES.HOME, true)).toBe(APP_ROUTES.HOME);
    expect(resolveNavigationPath(APP_ROUTES.ADMIN_DASHBOARD, true)).toBe(APP_ROUTES.ADMIN_DASHBOARD);
  });
});

