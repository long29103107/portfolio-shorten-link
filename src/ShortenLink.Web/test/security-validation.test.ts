import { describe, expect, test } from "bun:test";
import { mapCustomRoleApiFieldErrors, validateCustomRoleForm } from "../src/features/short-links/domain/securityValidation";

describe("security role validation", () => {
  test("maps required custom-role values to exact controls", () => {
    expect(validateCustomRoleForm({ id: "", name: " ", permissions: [] })).toEqual({
      id: "Enter a stable role id.",
      name: "Enter a role name."
    });
  });

  test("maps known API fields and ignores unknown fallback fields", () => {
    expect(mapCustomRoleApiFieldErrors({ id: "Required", permissions: "Unknown", other: "Fallback" })).toEqual({
      id: "Required",
      permissions: "Unknown"
    });
  });
});
