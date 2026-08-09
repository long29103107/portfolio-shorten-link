import { describe, expect, test } from "bun:test";
import {
  EXPIRING_SOON_THRESHOLD_MS,
  EXPIRY_PRESETS,
  createExpiryPresetValue,
  formatExpiryDateTime,
  getExpiryPresentation,
  toDateTimeLocalValue
} from "../src/features/short-links/domain/expiryPresentation";
import { validateShortLinkForm } from "../src/features/short-links/domain/validation";

const referenceTime = new Date("2026-07-30T00:00:00.000Z");

describe("expiry presentation", () => {
  test("gives lifecycle states precedence over the soon-expiry cue", () => {
    const soon = new Date(referenceTime.getTime() + EXPIRING_SOON_THRESHOLD_MS).toISOString();

    expect(getExpiryPresentation({ expiredAtUtc: soon, isActive: false }, referenceTime).state).toBe("inactive");
    expect(getExpiryPresentation({ expiredAtUtc: soon, isActive: true, isDeleted: true }, referenceTime).state).toBe("deleted");
    expect(getExpiryPresentation({ expiredAtUtc: "2026-07-29T23:59:59.000Z", isActive: true }, referenceTime).state).toBe("expired");
  });

  test("classifies the threshold and adjacent values deterministically", () => {
    const atThreshold = new Date(referenceTime.getTime() + EXPIRING_SOON_THRESHOLD_MS).toISOString();
    const justOutside = new Date(referenceTime.getTime() + EXPIRING_SOON_THRESHOLD_MS + 1).toISOString();
    const justInside = new Date(referenceTime.getTime() + EXPIRING_SOON_THRESHOLD_MS - 1).toISOString();

    expect(getExpiryPresentation({ expiredAtUtc: atThreshold, isActive: true }, referenceTime).state).toBe("expiring-soon");
    expect(getExpiryPresentation({ expiredAtUtc: justInside, isActive: true }, referenceTime).state).toBe("expiring-soon");
    expect(getExpiryPresentation({ expiredAtUtc: justOutside, isActive: true }, referenceTime).state).toBe("active");
  });

  test("handles missing and malformed dates without throwing", () => {
    expect(getExpiryPresentation({ expiredAtUtc: null, isActive: true }, referenceTime)).toMatchObject({
      state: "unknown",
      label: "No expiry"
    });
    expect(getExpiryPresentation({ expiredAtUtc: "not-a-date", isActive: true }, referenceTime)).toMatchObject({
      state: "unknown",
      label: "Expiry unavailable"
    });
  });

  test("formats expiry with explicit timezone context", () => {
    const formatted = formatExpiryDateTime("2026-07-30T00:00:00.000Z");
    expect(formatted).toMatch(/\b[A-Z]{2,5}\b|GMT[+-]/);
  });

  test("keeps the five create/edit presets shared and deterministic", () => {
    expect(EXPIRY_PRESETS.map((preset) => preset.label)).toEqual([
      "+30m", "+60m", "+180m", "+6h", "+12h"
    ]);

    for (const preset of EXPIRY_PRESETS) {
      const localValue = createExpiryPresetValue(referenceTime, preset.minutes);
      expect(new Date(localValue).getTime()).toBe(
        referenceTime.getTime() + preset.minutes * 60_000
      );
      expect(validateShortLinkForm({
        originalUrl: "https://example.com/docs",
        activeFromLocal: "",
        expiredAtLocal: localValue,
        maxClicksLocal: ""
      }, referenceTime)).toEqual({});
    }
  });

  test("round-trips a local datetime value across the browser offset", () => {
    const target = new Date("2026-12-31T23:45:00.000Z");
    const localValue = toDateTimeLocalValue(target);
    expect(new Date(localValue).getTime()).toBe(target.getTime());
  });
});
