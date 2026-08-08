import { describe, expect, test } from "bun:test";
import { createShortLinkQrDataUrl, getShortLinkQrPayload } from "../src/features/short-links/domain/qr";

describe("short-link QR presentation", () => {
  test("encodes only the authorized public short URL", () => {
    expect(getShortLinkQrPayload("  https://short.test/abc1234  "))
      .toBe("https://short.test/abc1234");
    expect(getShortLinkQrPayload("https://short.test/abc1234"))
      .not.toContain("destination");
  });

  test("generates a PNG data URL for the short URL", async () => {
    const dataUrl = await createShortLinkQrDataUrl("https://short.test/abc1234");

    expect(dataUrl.startsWith("data:image/png;base64,")).toBe(true);
    expect(dataUrl.length).toBeGreaterThan(100);
  });

  test("rejects a missing short URL", () => {
    expect(() => getShortLinkQrPayload("   ")).toThrow();
  });
});
