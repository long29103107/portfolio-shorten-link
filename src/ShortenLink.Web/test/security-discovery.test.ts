import { describe, expect, test } from "bun:test";
import { discoverPermissionGroups, discoverSecurityRoles, discoverSecurityUsers, getVisiblePages, paginateItems } from "../src/features/short-links/domain/securityDiscovery";
import type { SecurityRole, SecurityUser } from "../src/features/short-links/types";

const users: SecurityUser[] = [
  { id: "1", username: "z@example.com", displayName: "Zed", roleIds: [], isEnabled: true, isHidden: false, isBootstrap: false, createdAtUtc: "2026-01-02T00:00:00Z" },
  { id: "2", username: "a@example.com", displayName: "Alice", roleIds: ["Admin"], isEnabled: false, isHidden: false, isBootstrap: false, createdAtUtc: "2026-01-01T00:00:00Z" }
];
const roles: SecurityRole[] = [
  { id: "admin", name: "Administrator", permissions: [], defaultPermissions: [], permissionOverrides: [], isSystem: true, isEnabled: true, canDelete: false, createdAtUtc: null },
  { id: "viewer", name: "Read only", permissions: [], defaultPermissions: [], permissionOverrides: [], isSystem: true, isEnabled: true, canDelete: false, createdAtUtc: null }
];

describe("security user discovery", () => {
  test("searches, filters, and sorts deterministically", () => {
    expect(discoverSecurityUsers(users, { search: "alice", status: "disabled", role: "all", sortBy: "email", direction: "asc" }).map((user) => user.id)).toEqual(["2"]);
    expect(discoverSecurityUsers(users, { search: "", status: "all", role: "all", sortBy: "email", direction: "asc" }).map((user) => user.id)).toEqual(["2", "1"]);
    expect(discoverSecurityUsers(users, { search: "", status: "all", role: "none", sortBy: "email", direction: "asc" }).map((user) => user.id)).toEqual(["1"]);
    expect(discoverSecurityUsers(users, { search: "", status: "all", role: "admin", sortBy: "email", direction: "asc" }).map((user) => user.id)).toEqual(["2"]);
  });

  test("paginates and creates compact page ranges", () => {
    expect(paginateItems([1, 2, 3, 4, 5], 2, 2)).toEqual([3, 4]);
    expect(getVisiblePages(5, 10)).toEqual([1, "gap", 4, 5, 6, "gap", 10]);
  });
});

describe("role and permission discovery", () => {
  test("finds roles by name or id without case sensitivity", () => {
    expect(discoverSecurityRoles(roles, "ADMIN").map((role) => role.id)).toEqual(["admin"]);
    expect(discoverSecurityRoles(roles, "read only").map((role) => role.id)).toEqual(["viewer"]);
    expect(discoverSecurityRoles(roles, "missing")).toEqual([]);
  });

  test("finds permissions by group, code, or description and removes empty groups", () => {
    const groups = [
      { id: "links", name: "Short links", permissions: ["short_links.read", "short_links.delete"] },
      { id: "audit", name: "Audit", permissions: ["audit_logs.read"] }
    ];
    const descriptions: Record<string, string> = {
      "short_links.read": "View links",
      "short_links.delete": "Permanently delete links",
      "audit_logs.read": "Review security activity"
    };
    expect(discoverPermissionGroups(groups, "DELETE", (permission) => descriptions[permission]).map((group) => group.permissions)).toEqual([["short_links.delete"]]);
    expect(discoverPermissionGroups(groups, "security activity", (permission) => descriptions[permission]).map((group) => group.id)).toEqual(["audit"]);
    expect(discoverPermissionGroups(groups, "short links", (permission) => descriptions[permission])[0].permissions).toEqual(["short_links.read", "short_links.delete"]);
    expect(discoverPermissionGroups(groups, "missing", (permission) => descriptions[permission])).toEqual([]);
  });
});
