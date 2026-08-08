import { useCallback, useEffect, useRef, useState } from "react";
import { getRateLimitActivity, listSecurityRoles, listSecurityUsers, listShortLinks } from "../api/shortLinksApi";
import {
  buildDashboardLinkDiscovery,
  composeDashboardSnapshot,
  type DashboardSnapshot,
  type DashboardSource
} from "../domain/adminDashboard";
import type { RateLimitActivity, ShortLinkStatusFilter } from "../types";
import { DASHBOARD_DEFAULTS } from "../constants/defaults";

function listLinksByStatus(
  status: ShortLinkStatusFilter,
  limit = DASHBOARD_DEFAULTS.LINK_LIMIT,
  signal?: AbortSignal
) {
  const request = buildDashboardLinkDiscovery(status, limit);
  return listShortLinks(request.limit, request.page, request.discovery, signal);
}

export function useAdminDashboardData() {
  const [snapshot, setSnapshot] = useState<DashboardSnapshot | null>(null);
  const [rateLimitActivity, setRateLimitActivity] = useState<RateLimitActivity | null>(null);
  const [rateLimitError, setRateLimitError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const requestVersion = useRef(0);
  const activeController = useRef<AbortController | null>(null);

  const loadDashboard = useCallback(async () => {
    activeController.current?.abort();
    const controller = new AbortController();
    activeController.current = controller;
    const version = ++requestVersion.current;
    setIsLoading(true);

    const [allLinks, activeLinks, inactiveLinks, users, roles, rateLimits] = await Promise.allSettled([
      listLinksByStatus("all", DASHBOARD_DEFAULTS.RECENT_LINK_LIMIT, controller.signal),
      listLinksByStatus("active", DASHBOARD_DEFAULTS.LINK_LIMIT, controller.signal),
      listLinksByStatus("inactive", DASHBOARD_DEFAULTS.LINK_LIMIT, controller.signal),
      listSecurityUsers(controller.signal),
      listSecurityRoles(controller.signal),
      getRateLimitActivity(controller.signal)
    ]);

    if (controller.signal.aborted || version !== requestVersion.current) {
      return;
    }

    const linksFailed = [allLinks, activeLinks, inactiveLinks].some(
      (result) => result.status === "rejected"
    );
    const failedSources: DashboardSource[] = [
      ...(linksFailed ? ["shortLinks" as const] : []),
      ...(users.status === "rejected" ? ["users" as const] : []),
      ...(roles.status === "rejected" ? ["roles" as const] : [])
    ];

    setSnapshot(composeDashboardSnapshot({
      totalLinks: allLinks.status === "fulfilled" ? allLinks.value.totalCount ?? undefined : undefined,
      activeLinks: activeLinks.status === "fulfilled" ? activeLinks.value.totalCount ?? undefined : undefined,
      deactivatedLinks: inactiveLinks.status === "fulfilled" ? inactiveLinks.value.totalCount ?? undefined : undefined,
      users: users.status === "fulfilled" ? users.value.items : undefined,
      shortLinks: allLinks.status === "fulfilled" ? allLinks.value.items : undefined,
      roles: roles.status === "fulfilled"
        ? roles.value.systemRoles.length + roles.value.customRoles.length
        : undefined,
      failedSources
    }));

    if (rateLimits.status === "fulfilled") {
      setRateLimitActivity(rateLimits.value);
      setRateLimitError(null);
    } else {
      setRateLimitActivity(null);
      setRateLimitError("Rate-limit activity is unavailable for this workspace.");
    }
    setIsLoading(false);
  }, []);

  useEffect(() => {
    void loadDashboard();
    return () => {
      requestVersion.current += 1;
      activeController.current?.abort();
    };
  }, [loadDashboard]);

  return {
    snapshot,
    rateLimitActivity,
    rateLimitError,
    isLoading,
    loadDashboard
  };
}
