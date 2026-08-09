import { expect, test } from "bun:test";
import { updateRolePermissionState } from "../src/features/short-links/components/RolePermissionMatrix";

const roleForm = {
  id: "auditor",
  name: "Auditor",
  permissions: ["audit_logs.read"],
  defaultPermissions: ["audit_logs.read", "analytics.read"],
  permissionOverrides: {},
  isEnabled: true
};

test("stages a permission override without mutating the persisted form", () => {
  const next = updateRolePermissionState(roleForm, ["analytics.read"], false);

  expect(next).toEqual({
    ...roleForm,
    permissions: ["audit_logs.read"],
    permissionOverrides: { "analytics.read": false }
  });
  expect(roleForm.permissionOverrides).toEqual({});
});

test("applies a group decision in stable permission order", () => {
  expect(updateRolePermissionState(roleForm, ["analytics.read", "audit_logs.read"], true)).toEqual({
    ...roleForm,
    permissions: ["audit_logs.read", "analytics.read"],
    permissionOverrides: {}
  });
});
