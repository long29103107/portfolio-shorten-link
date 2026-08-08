import { describe, expect, test } from "bun:test";
import { serializeShortLinksCsv } from "../src/features/short-links/domain/export";
import type { ShortLinkAdminItem } from "../src/features/short-links/types";

describe("short-link CSV export", () => {
  test("keeps a stable header and escapes comma, quote, and newline cells", () => {
    expect(serializeShortLinksCsv([link({
      originalUrl: "https://example.test/a,b\"c\nd",
      createdByDisplayName: "Ada, \"Admin\""
    })])).toBe([
      "Code,Short URL,Destination URL,Created At (UTC),Expires At (UTC),Status,Access,Created By",
      "abc1234,https://short.test/abc1234,\"https://example.test/a,b\"\"c\nd\",2026-07-29T00:00:00Z,2026-08-01T00:00:00Z,Active,Owner,\"Ada, \"\"Admin\"\"\"",
      ""
    ].join("\r\n"));
  });

  test("exports only the safe short-link fields in server order", () => {
    const csv = serializeShortLinksCsv([
      link({ code: "first", isActive: false }),
      link({ code: "second", expiredAtUtc: null, accessLevel: "View" })
    ]);

    expect(csv.split("\r\n").slice(1, 3).map((row) => row.split(",")[0]))
      .toEqual(["first", "second"]);
    expect(csv).not.toContain("password");
    expect(csv).not.toContain("api-key-secret");
  });
});

function link(overrides: Partial<ShortLinkAdminItem> = {}): ShortLinkAdminItem {
  return {
    code: "abc1234",
    shortUrl: "https://short.test/abc1234",
    originalUrl: "https://example.test/destination",
    createdAtUtc: "2026-07-29T00:00:00Z",
    expiredAtUtc: "2026-08-01T00:00:00Z",
    isActive: true,
    createdByUserId: "user-1",
    createdByDisplayName: null,
    createdByUsername: "admin",
    accessLevel: "Owner",
    ...overrides
  };
}
