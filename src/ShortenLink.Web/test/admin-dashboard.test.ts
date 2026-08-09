import { describe, expect, test } from "bun:test";
import { buildDashboardLinkDiscovery, composeDashboardSnapshot, composeRecentActivity } from "../src/features/short-links/domain/adminDashboard";
import { buildShortLinkListUrl } from "../src/features/short-links/api/shortLinksApi";
import type { SecurityUser, ShortLinkAdminItem } from "../src/features/short-links/types";

const users: SecurityUser[] = [
  {
    id: "1", username: "admin", displayName: "Admin", roleIds: ["admin"],
    isEnabled: true, isHidden: false, isBootstrap: true, createdAtUtc: "2026-07-24T00:00:00Z"
  },
  {
    id: "2", username: "disabled", displayName: "Disabled", roleIds: [],
    isEnabled: false, isHidden: false, isBootstrap: false, createdAtUtc: "2026-07-24T00:00:00Z"
  }
];

describe("admin dashboard snapshot", () => {
  test("builds dashboard link reads with the requested limit on page one", () => {
    const request = buildDashboardLinkDiscovery("all", 6);

    expect(request).toEqual({
      limit: 6,
      page: 1,
      discovery: {
        search: "",
        status: "all",
        sortBy: "created",
        sortDirection: "desc",
        folder: "",
        tag: ""
      }
    });
    expect(buildShortLinkListUrl(request.limit, request.page, request.discovery)).toBe(
      "/api/short-links?limit=6&page=1&sort=-CreatedAt"
    );
  });

  test("composes authoritative totals and enabled user count", () => {
    expect(composeDashboardSnapshot({
      totalLinks: 30, activeLinks: 22, deactivatedLinks: 8, users, roles: 4
    })).toEqual({
      totalLinks: 30,
      activeLinks: 22,
      deactivatedLinks: 8,
      users: 2,
      enabledUsers: 1,
      roles: 4,
      recentActivity: [
        {
          id: "user:1",
          kind: "user",
          title: "Admin registered",
          detail: "admin",
          occurredAtUtc: "2026-07-24T00:00:00Z"
        },
        {
          id: "user:2",
          kind: "user",
          title: "Disabled registered",
          detail: "disabled",
          occurredAtUtc: "2026-07-24T00:00:00Z"
        }
      ],
      health: { shortLinks: "healthy", users: "healthy", roles: "healthy" }
    });
  });

  test("keeps healthy identity metrics when short-link discovery fails", () => {
    const snapshot = composeDashboardSnapshot({
      users, roles: 4, failedSources: ["shortLinks"]
    });

    expect(snapshot.totalLinks).toBeUndefined();
    expect(snapshot.users).toBe(2);
    expect(snapshot.roles).toBe(4);
    expect(snapshot.health).toEqual({
      shortLinks: "failed", users: "healthy", roles: "healthy"
    });
  });

  test("merges recent link and user creation newest first with a stable limit", () => {
    const links: ShortLinkAdminItem[] = [{
      code: "abc1234",
      shortUrl: "https://sho.rt/abc1234",
      originalUrl: "https://example.com",
      createdAtUtc: "2026-07-24T03:00:00Z",
      expiredAtUtc: null,
      isActive: true,
      maxClicks: null,
      clickCount: 0,
      createdByUserId: "1",
      createdByDisplayName: "Admin",
      createdByUsername: "admin@shortenlink.local",
      accessLevel: "Admin"
    }];

    expect(composeRecentActivity(links, users, 2).map((event) => event.id)).toEqual([
      "short-link:abc1234",
      "user:1"
    ]);
  });
});
