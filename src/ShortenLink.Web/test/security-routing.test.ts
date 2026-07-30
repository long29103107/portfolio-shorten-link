import { describe, expect, test } from "bun:test";
import { parseRoute } from "../src/app/router";
import { toFriendlyErrorMessage } from "../src/features/short-links/types";

describe("security navigation", () => {
  test("parses the login route", () => {
    expect(parseRoute("/login")).toEqual({ kind: "login" });
  });

  test("routes admin security sidebar destinations", () => {
    expect(parseRoute("/short-links")).toEqual({ kind: "admin" });
    expect(parseRoute("/admin/dashboard")).toEqual({ kind: "dashboard" });
    expect(parseRoute("/audit-logs")).toEqual({ kind: "audit" });
    expect(parseRoute("/admin")).toEqual({ kind: "status", statusCode: 404 });
    expect(parseRoute("/admin/security")).toEqual({ kind: "security", section: "users" });
    expect(parseRoute("/admin/security/users")).toEqual({ kind: "security", section: "users" });
    expect(parseRoute("/admin/security/roles")).toEqual({ kind: "security", section: "roles" });
    expect(parseRoute("/admin/security/permissions")).toEqual({ kind: "status", statusCode: 404 });
    expect(parseRoute("/security/users")).toEqual({ kind: "status", statusCode: 404 });
  });

  test("maps invalid login errors to form-friendly copy", () => {
    expect(toFriendlyErrorMessage("invalid_login", "fallback")).toBe("Username or password is invalid.");
  });
});
