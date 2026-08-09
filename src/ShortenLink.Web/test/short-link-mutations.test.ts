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
    expiredAtLocal: "2026-08-08T14:30"
  };

  expect(buildShortLinkMutationPayload(form)).toEqual({
    originalUrl: "https://example.com/docs",
    activeFromUtc: null,
    expiredAtUtc: new Date(form.expiredAtLocal).toISOString()
  });
});

test("converts API expiry values back to editor-local values", () => {
  const apiValue = "2026-08-08T07:30:00.000Z";
  expect(toEditorExpiryValue(apiValue)).toBe(toDateTimeLocalValue(new Date(apiValue)));
  expect(toEditorExpiryValue(null)).toBe("");
  expect(toEditorExpiryValue("not-a-date")).toBe("");
});
