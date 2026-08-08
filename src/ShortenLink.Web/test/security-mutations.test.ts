import { expect, test } from "bun:test";
import {
  buildRolePermissionOverridesRequest,
  toRoleForm
} from "../src/features/short-links/hooks/useSecurityMutations";

test("maps persisted role state into an editable form without losing overrides", () => {
  const role = {
    id: "auditor",
    name: "Auditor",
    permissions: ["audit_logs.read"],
    defaultPermissions: ["audit_logs.read", "analytics.read"],
    permissionOverrides: [{ permission: "analytics.read", isAllowed: false }],
    isSystem: false,
    isEnabled: true,
    canDelete: true,
    createdAtUtc: "2026-08-08T00:00:00.000Z"
  };

  expect(toRoleForm(role)).toEqual({
    id: "auditor",
    name: "Auditor",
    permissions: ["audit_logs.read"],
    defaultPermissions: ["audit_logs.read", "analytics.read"],
    permissionOverrides: { "analytics.read": false },
    isEnabled: true
  });
});

test("serializes staged role overrides at the API boundary", () => {
  expect(buildRolePermissionOverridesRequest({
    id: "auditor",
    name: "Auditor",
    permissions: ["audit_logs.read"],
    defaultPermissions: ["audit_logs.read"],
    permissionOverrides: {
      "analytics.read": false,
      "audit_logs.read": true
    },
    isEnabled: true
  })).toEqual({
    overrides: [
      { permission: "analytics.read", isAllowed: false },
      { permission: "audit_logs.read", isAllowed: true }
    ]
  });
});
