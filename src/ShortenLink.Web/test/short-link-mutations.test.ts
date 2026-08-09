import { expect, test } from "bun:test";
import {
  buildShortLinkMutationPayload,
  toEditorExpiryValue
} from "../src/features/short-links/hooks/useShortLinkMutations";
import { toDateTimeLocalValue } from "../src/features/short-links/domain/expiryPresentation";

test("builds the API payload from normalized editor values", () => {
  const form = {
    originalUrl: "  https://example.com/docs  ",
    activeFromLocal: "",
    expiredAtLocal: "2026-08-08T14:30",
    maxClicksLocal: "3"
  };

  expect(buildShortLinkMutationPayload(form)).toEqual({
    originalUrl: "https://example.com/docs",
    activeFromUtc: null,
    expiredAtUtc: new Date(form.expiredAtLocal).toISOString(),
    maxClicks: 3
  });
});

test("converts API expiry values back to editor-local values", () => {
  const apiValue = "2026-08-08T07:30:00.000Z";
  expect(toEditorExpiryValue(apiValue)).toBe(toDateTimeLocalValue(new Date(apiValue)));
  expect(toEditorExpiryValue(null)).toBe("");
  expect(toEditorExpiryValue("not-a-date")).toBe("");
});

test("serializes a replacement password and explicit protection removal", () => {
  expect(buildShortLinkMutationPayload({
    originalUrl: "https://example.com/docs",
    activeFromLocal: "",
    expiredAtLocal: "2026-08-08T14:30",
    maxClicksLocal: "",
    passwordLocal: "new-link-password",
    clearPassword: false
  })).toMatchObject({
    password: "new-link-password"
  });

  expect(buildShortLinkMutationPayload({
    originalUrl: "https://example.com/docs",
    activeFromLocal: "",
    expiredAtLocal: "2026-08-08T14:30",
    maxClicksLocal: "",
    passwordLocal: "",
    clearPassword: true
  })).toMatchObject({
    clearPassword: true
  });
});
