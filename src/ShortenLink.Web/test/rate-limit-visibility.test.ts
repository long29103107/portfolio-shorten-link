import { describe, expect, test } from "bun:test";
import { buildRateLimitPolicyViews } from "../src/features/short-links/rateLimitPresentation";
import type { RateLimitActivity } from "../src/features/short-links/types";

describe("rate-limit visibility", () => {
  test("presents only policy configuration and aggregate rejection counts", () => {
    expect(buildRateLimitPolicyViews(activity())).toEqual([
      { label: "Create", permitLimit: "3", window: "11s", queueLimit: "0", rejectedCount: 2 },
      { label: "Redirect", permitLimit: "7", window: "13s", queueLimit: "1", rejectedCount: 4 }
    ]);
  });

  test("does not expose request-sensitive fields in the policy view", () => {
    const view = JSON.stringify(buildRateLimitPolicyViews(activity()));
    expect(view).not.toContain("remoteIp");
    expect(view).not.toContain("shortCode");
    expect(view).not.toContain("requestUrl");
  });
});

function activity(): RateLimitActivity {
  return {
    enabled: true,
    create: { permitLimit: 3, windowSeconds: 11, queueLimit: 0, rejectedCount: 2 },
    redirect: { permitLimit: 7, windowSeconds: 13, queueLimit: 1, rejectedCount: 4 },
    recentRejections: [{ policy: "create", occurredAtUtc: "2026-07-29T00:00:00Z" }]
  };
}
