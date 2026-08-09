import { startTransition, useCallback, useEffect, useState } from "react";
import type { AppRoute } from "@/features/short-links/types";
import { getStoredSessionToken } from "@/features/short-links/api/adminSecurity";
import { APP_ROUTES, buildSecurityRoute } from "@/shared/constants/routes";
import { parseRoute } from "../router";

type UseAppNavigationOptions = {
  hasDirtyChanges: boolean;
  onDiscardChanges: () => void;
};

export function getInitialAppRoute(): AppRoute {
  return getStoredSessionToken() ? parseRoute(window.location.pathname) : { kind: "login" };
}

export function getCurrentRoutePath(route: AppRoute, currentPathname: string): string {
  return route.kind === "admin"
    ? APP_ROUTES.SHORT_LINKS
    : route.kind === "security"
      ? buildSecurityRoute(route.section)
      : route.kind === "audit"
        ? APP_ROUTES.AUDIT_LOGS
        : route.kind === "dashboard"
          ? APP_ROUTES.ADMIN_DASHBOARD
          : currentPathname;
}

export function resolveNavigationPath(requestedPath: string, hasSession: boolean): string {
  return !hasSession && requestedPath !== APP_ROUTES.LOGIN
    ? APP_ROUTES.LOGIN
    : requestedPath;
}

export function useAppNavigation({ hasDirtyChanges, onDiscardChanges }: UseAppNavigationOptions) {
  const [route, setRoute] = useState<AppRoute>(getInitialAppRoute);
  const [pendingNavigationPath, setPendingNavigationPath] = useState<string | null>(null);

  const transitionTo = useCallback((path: string) => {
    startTransition(() => {
      setRoute(parseRoute(path));
    });
  }, []);

  const forceNavigate = useCallback((path: string) => {
    if (window.location.pathname !== path) {
      window.history.replaceState({}, "", path);
    }
    setPendingNavigationPath(null);
    transitionTo(path);
  }, [transitionTo]);

  const commitNavigation = useCallback((requestedPath: string) => {
    const path = resolveNavigationPath(requestedPath, Boolean(getStoredSessionToken()));
    if (window.location.pathname !== path) {
      window.history.pushState({}, "", path);
    }
    transitionTo(path);
  }, [transitionTo]);

  const navigate = useCallback((path: string) => {
    if (hasDirtyChanges && path !== window.location.pathname) {
      setPendingNavigationPath(path);
      return;
    }

    commitNavigation(path);
  }, [commitNavigation, hasDirtyChanges]);

  const confirmDiscardAndNavigate = useCallback(() => {
    if (!pendingNavigationPath) {
      return;
    }

    onDiscardChanges();
    commitNavigation(pendingNavigationPath);
    setPendingNavigationPath(null);
  }, [commitNavigation, onDiscardChanges, pendingNavigationPath]);

  useEffect(() => {
    const handleBeforeUnload = (event: BeforeUnloadEvent) => {
      if (!hasDirtyChanges) return;
      event.preventDefault();
      event.returnValue = "";
    };

    window.addEventListener("beforeunload", handleBeforeUnload);
    return () => window.removeEventListener("beforeunload", handleBeforeUnload);
  }, [hasDirtyChanges]);

  useEffect(() => {
    const handlePopState = () => {
      const nextPath = window.location.pathname;
      if (!getStoredSessionToken() && nextPath !== APP_ROUTES.LOGIN) {
        forceNavigate(APP_ROUTES.LOGIN);
        return;
      }

      const currentPath = getCurrentRoutePath(route, window.location.pathname);
      if (hasDirtyChanges && nextPath !== currentPath) {
        window.history.pushState({}, "", currentPath);
        setPendingNavigationPath(nextPath);
        transitionTo(currentPath);
        return;
      }

      transitionTo(nextPath);
    };

    window.addEventListener("popstate", handlePopState);
    return () => window.removeEventListener("popstate", handlePopState);
  }, [forceNavigate, hasDirtyChanges, route, transitionTo]);

  return {
    route,
    pendingNavigationPath,
    navigate,
    forceNavigate,
    confirmDiscardAndNavigate,
    cancelPendingNavigation: () => setPendingNavigationPath(null)
  };
}

