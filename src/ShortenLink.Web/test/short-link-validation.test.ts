import { describe, expect, test } from "bun:test";
import {
  mapShortLinkApiFieldErrors,
  validateShortLinkForm
} from "../src/features/short-links/domain/validation";

const now = new Date("2026-07-21T00:00:00.000Z");

describe("short-link validation parity", () => {
  test("requires an absolute HTTP(S) destination and future expiration", () => {
    expect(validateShortLinkForm({ originalUrl: "", activeFromLocal: "", expiredAtLocal: "", maxClicksLocal: "" }, now)).toEqual({
      originalUrl: "Paste a full destination URL to shorten.",
      expiredAtLocal: "Choose an expiry time."
    });
    expect(validateShortLinkForm({
      originalUrl: "ftp://example.com/file",
      activeFromLocal: "",
      expiredAtLocal: "2026-07-20T23:59:59.000Z",
      maxClicksLocal: ""
    }, now)).toEqual({
      originalUrl: "Use an http:// or https:// link.",
      expiredAtLocal: "Choose an expiry time in the future."
    });
  });

  test("accepts deterministic values accepted by the backend", () => {
    expect(validateShortLinkForm({
      originalUrl: " https://example.com/docs ",
      activeFromLocal: "",
      expiredAtLocal: "2026-07-21T00:00:01.000Z",
      maxClicksLocal: ""
    }, now)).toEqual({});
  });
});

describe("short-link API field mapping", () => {
  test("maps API request fields to local controls", () => {
    expect(mapShortLinkApiFieldErrors({
      originalUrl: "Server URL message",
      expiredAtUtc: "Server expiry message"
    })).toEqual({
      originalUrl: "Server URL message",
      expiredAtLocal: "Server expiry message"
    });
  });

  test("ignores unknown fields for form-level fallback", () => {
    expect(mapShortLinkApiFieldErrors({ code: "Unknown field" })).toEqual({});
  });

  test("validates a positive click limit", () => {
    expect(validateShortLinkForm({
      originalUrl: "https://example.com/docs",
      activeFromLocal: "",
      expiredAtLocal: "2026-07-21T00:00:01.000Z",
      maxClicksLocal: "0"
    }, now).maxClicksLocal).toContain("positive");
  });

  test("limits optional link passwords", () => {
    expect(validateShortLinkForm({
      originalUrl: "https://example.com/docs",
      activeFromLocal: "",
      expiredAtLocal: "2026-07-21T00:00:01.000Z",
      maxClicksLocal: "",
      passwordLocal: "x".repeat(257),
      clearPassword: false
    }, now).passwordLocal).toContain("256");
  });
});
