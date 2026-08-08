import type {
  SecurityUser,
  ShortLinkAdminItem,
  ShortLinkDiscoveryQuery,
  ShortLinkStatusFilter
} from "../types";

export type DashboardSource = "shortLinks" | "users" | "roles";
export type DashboardSourceState = "healthy" | "failed";

export type DashboardSnapshot = {
  totalLinks?: number;
  activeLinks?: number;
  deactivatedLinks?: number;
  users?: number;
  enabledUsers?: number;
  roles?: number;
  recentActivity: DashboardActivity[];
  health: Record<DashboardSource, DashboardSourceState>;
};

export type DashboardActivity = {
  id: string;
  kind: "shortLink" | "user";
  title: string;
  detail: string;
  occurredAtUtc: string;
};

export type DashboardLinkDiscovery = {
  limit: number;
  page: number;
  discovery: ShortLinkDiscoveryQuery;
};

export function buildDashboardLinkDiscovery(
  status: ShortLinkStatusFilter,
  limit: number
): DashboardLinkDiscovery {
  return {
    limit,
    page: 1,
    discovery: {
      search: "",
      status,
      sortBy: "created",
      sortDirection: "desc"
    }
  };
}

type DashboardSnapshotInput = {
  totalLinks?: number;
  activeLinks?: number;
  deactivatedLinks?: number;
  users?: SecurityUser[];
  shortLinks?: ShortLinkAdminItem[];
  roles?: number;
  failedSources?: DashboardSource[];
};

export function composeDashboardSnapshot({
  totalLinks,
  activeLinks,
  deactivatedLinks,
  users,
  shortLinks,
  roles,
  failedSources = []
}: DashboardSnapshotInput): DashboardSnapshot {
  const failed = new Set(failedSources);

  return {
    totalLinks,
    activeLinks,
    deactivatedLinks,
    users: users?.length,
    enabledUsers: users?.filter((user) => user.isEnabled).length,
    roles,
    recentActivity: composeRecentActivity(shortLinks, users),
    health: {
      shortLinks: failed.has("shortLinks") ? "failed" : "healthy",
      users: failed.has("users") ? "failed" : "healthy",
      roles: failed.has("roles") ? "failed" : "healthy"
    }
  };
}

export function composeRecentActivity(
  shortLinks: ShortLinkAdminItem[] = [],
  users: SecurityUser[] = [],
  limit = 6
): DashboardActivity[] {
  return [
    ...shortLinks.map((link): DashboardActivity => ({
      id: `short-link:${link.code}`,
      kind: "shortLink",
      title: `Short link ${link.code} created`,
      detail: link.createdByDisplayName || link.createdByUsername
        ? `Created by ${link.createdByDisplayName || link.createdByUsername}`
        : "Creator unavailable",
      occurredAtUtc: link.createdAtUtc
    })),
    ...users.map((user): DashboardActivity => ({
      id: `user:${user.id}`,
      kind: "user",
      title: `${user.displayName || user.username} registered`,
      detail: user.username,
      occurredAtUtc: user.createdAtUtc
    }))
  ]
    .sort((left, right) =>
      Date.parse(right.occurredAtUtc) - Date.parse(left.occurredAtUtc)
      || left.id.localeCompare(right.id))
    .slice(0, Math.max(0, limit));
}
