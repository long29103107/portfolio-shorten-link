import { describe, expect, test } from "bun:test";
import { classifyHttpFailure } from "../src/shared/api/apiFailure";
import {
  hasFieldErrors,
  mapLoginApiFieldErrors,
  mapManagedUserApiFieldErrors,
  mapRoleAssignmentApiFieldErrors,
  validatePasswordReset,
  validateLoginForm,
  validateManagedUserForm
} from "../src/features/short-links/domain/identityValidation";

describe("identity form validation", () => {
  test("maps required login values to exact controls", () => {
    expect(validateLoginForm({ email: " ", password: "" })).toEqual({
      email: "Enter your email.",
      password: "Enter your password."
    });
  });

  test("requires email and display name while registering a managed user", () => {
    expect(validateManagedUserForm({ email: "invalid", displayName: "", password: "" })).toEqual({
      email: "Enter a valid email address.",
      displayName: "Enter a display name."
    });
    expect(validateManagedUserForm({ email: "editor@example.com", displayName: "Editor", password: "secret" })).toEqual({});
  });

  test("maps known login and managed-user API fields without unknown fields", () => {
    expect(mapLoginApiFieldErrors({ email: "Required", other: "Fallback" })).toEqual({ email: "Required" });
    expect(mapManagedUserApiFieldErrors({ username: "Duplicate", displayName: "Required", other: "Fallback" })).toEqual({
      displayName: "Required",
      email: "Duplicate"
    });
    expect(mapRoleAssignmentApiFieldErrors({ roleIds: "Unknown role", other: "Fallback" })).toEqual({ roleIds: "Unknown role" });
    expect(validatePasswordReset(" ")).toEqual({ password: "Enter a new password." });
  });

  test("normalizes the backend array-valued field error contract", () => {
    const failure = classifyHttpFailure(400, {
      errorCode: "invalid_security_user",
      message: "Invalid user",
      fieldErrors: { username: ["Username is required."] }
    });
    expect(failure.fieldErrors).toEqual({ username: "Username is required." });
    expect(hasFieldErrors(failure.fieldErrors)).toBe(true);
  });
});
