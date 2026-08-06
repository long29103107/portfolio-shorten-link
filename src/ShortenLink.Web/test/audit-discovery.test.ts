import { describe, expect, test } from "bun:test";
import {
  buildAuditLogUrl,
  formatAuditLabel,
  mergeAuditLogEvents,
  toAuditFilterIso,
  validateAuditTimeRange
} from "../src/features/short-links/auditDiscovery";
import type { AuditLogEvent } from "../src/features/short-links/types";
import { createRecoveryNotice } from "../src/shared/api/recovery";

describe("audit investigation discovery", () => {
  test("serializes filters and opaque cursor exactly once", () => {
    expect(buildAuditLogUrl({
      limit: 500,
      cursor: " opaque+/= ",
      filters: {
        action: " short_link.updated ",
        targetId: " abc 123 ",
        actorId: " user@example.com ",
        from: "2026-07-01T00:00:00.000Z",
        to: "2026-07-31T23:59:59.000Z"
      }
    })).toBe(
      "/api/audit-logs?limit=200&cursor=opaque%2B%2F%3D&fe=%28%28Action+eq+%60short_link.updated%60%29+%26+%28TargetId+eq+%60abc+123%60%29+%26+%28ActorId+eq+%60user%40example.com%60%29+%26+%28OccurredAt+ge+%602026-07-01T00%3A00%3A00.000Z%60%29+%26+%28OccurredAt+le+%602026-07-31T23%3A59%3A59.000Z%60%29%29"
    );
  });

  test("serializes a selected action for the audit API", () => {
    expect(buildAuditLogUrl({ filters: { action: "short_link.created" } }))
      .toBe("/api/audit-logs?limit=50&fe=%28Action+eq+%60short_link.created%60%29");
  });

  test("omits blank filters and uses the newest-page defaults", () => {
    expect(buildAuditLogUrl()).toBe("/api/audit-logs?limit=50");
  });

  test("appends older pages without duplicates and preserves server order", () => {
    const first = auditEvent("1", "short_link.updated");
    const duplicate = auditEvent("1", "short_link.updated");
    const older = auditEvent("2", "authentication.login");

    expect(mergeAuditLogEvents([first], [duplicate, older]).map((event) => event.id))
      .toEqual(["1", "2"]);
  });

  test("validates local time ranges and emits ISO filter values", () => {
    expect(validateAuditTimeRange("2026-07-28T10:00", "2026-07-28T09:00"))
      .toBe("From must be earlier than or equal to To.");
    expect(validateAuditTimeRange("2026-07-28T09:00", "2026-07-28T10:00"))
      .toBeNull();
    expect(toAuditFilterIso("2026-07-28T09:00"))
      .toBe(new Date("2026-07-28T09:00").toISOString());
    expect(toAuditFilterIso("")).toBe("");
  });

  test("formats stable contract values and preserves retry guidance", () => {
    expect(formatAuditLabel("security_role.permissions_replaced"))
      .toBe("Security Role Permissions Replaced");
    expect(createRecoveryNotice(
      { retryable: true },
      "Older audit events could not be loaded."
    )).toEqual({
      message: "Older audit events could not be loaded.",
      retryable: true
    });
  });
});

function auditEvent(id: string, action: string): AuditLogEvent {
  return {
    id,
    actorId: "user-1",
    action,
    targetType: "short_link",
    targetId: "abc1234",
    ownerUserId: "user-1",
    outcome: "succeeded",
    occurredAtUtc: "2026-07-28T00:00:00Z",
    subjectUserId: null,
    detail: null
  };
}
