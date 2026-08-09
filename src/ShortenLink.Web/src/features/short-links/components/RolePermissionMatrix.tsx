import { useEffect, useState } from "react";
import type { CSSProperties } from "react";
import { shortLinkPermissions } from "../api/adminSecurity";
import type { SecurityRole } from "../types";
import { discoverPermissionGroups, discoverSecurityRoles } from "../domain/securityDiscovery";
import { getPermissionDescription } from "../domain/permissionCatalog";
import type { CustomRoleFieldErrors } from "../domain/securityValidation";
import { Badge } from "@/shared/components/ui/badge";
import { Button } from "@/shared/components/ui/button";
import { ConfirmDialog } from "@/shared/components/ConfirmDialog";
import { Input } from "@/shared/components/ui/input";
import { toRoleForm, type RoleFormState } from "../hooks/useSecurityMutations";

const permissionOptions = Object.values(shortLinkPermissions);
const permissionGroups = [
  { id: "short-links", name: "Short links", permissions: permissionOptions.filter((permission) => permission.startsWith("short_links.")) },
  { id: "reporting", name: "Reporting and audit", permissions: permissionOptions.filter((permission) => permission === "analytics.read" || permission === "audit_logs.read") },
  { id: "security", name: "Security", permissions: permissionOptions.filter((permission) => permission.startsWith("security.")) }
];

type RolePermissionMatrixProps = {
  roles: SecurityRole[];
  form: RoleFormState;
  errors: CustomRoleFieldErrors;
  isSaving: boolean;
  onDirtyChange: (isDirty: boolean) => void;
  onFormChange: (form: RoleFormState) => void;
  onErrorsChange: (errors: CustomRoleFieldErrors) => void;
  onSave: (drafts: RoleFormState[]) => Promise<boolean>;
};

export function updateRolePermissionState(
  form: RoleFormState,
  permissionsToChange: string[],
  allowed: boolean
): RoleFormState {
  const permissionSet = new Set(form.permissions);
  const permissionOverrides = { ...form.permissionOverrides };

  permissionsToChange.forEach((permission) => {
    const defaultAllowed = form.defaultPermissions.includes(permission);
    if (allowed) permissionSet.add(permission);
    else permissionSet.delete(permission);
    if (allowed === defaultAllowed) delete permissionOverrides[permission];
    else permissionOverrides[permission] = allowed;
  });

  return { ...form, permissions: Array.from(permissionSet), permissionOverrides };
}

export function RolePermissionMatrix({
  roles,
  form,
  errors,
  isSaving,
  onDirtyChange,
  onFormChange,
  onErrorsChange,
  onSave
}: RolePermissionMatrixProps) {
  const selectedRole = roles.find((role) => role.id === form.id);
  const [roleSearch, setRoleSearch] = useState("");
  const [permissionSearch, setPermissionSearch] = useState("");
  const [expandedPermissionGroups, setExpandedPermissionGroups] = useState<Record<string, boolean>>(
    () => Object.fromEntries(permissionGroups.map((group) => [group.id, true]))
  );
  const [isSaveConfirmationOpen, setIsSaveConfirmationOpen] = useState(false);
  const [roleDrafts, setRoleDrafts] = useState<Record<string, RoleFormState>>({});
  const hasChanges = (draft: RoleFormState) => {
    const role = roles.find((item) => item.id === draft.id);
    if (!role) return false;
    const persisted = Object.fromEntries(role.permissionOverrides.map((item) => [item.permission, item.isAllowed]));
    return permissionOptions.some((permission) => draft.permissionOverrides[permission] !== persisted[permission]);
  };
  const dirtyRoleDrafts = Object.values(roleDrafts).filter(hasChanges);
  const hasPermissionChanges = dirtyRoleDrafts.length > 0;

  useEffect(() => {
    onDirtyChange(hasPermissionChanges);
  }, [hasPermissionChanges, onDirtyChange]);

  const updateRoleDraft = (nextForm: RoleFormState) => {
    setRoleDrafts((current) => ({ ...current, [nextForm.id]: nextForm }));
    onFormChange(nextForm);
  };
  const visibleRoles = discoverSecurityRoles(roles, roleSearch);
  const normalizedPermissionSearch = permissionSearch.trim().toLowerCase();
  const visiblePermissionGroups = discoverPermissionGroups(permissionGroups, permissionSearch, getPermissionDescription);
  const setPermission = (permission: string, allowed: boolean) => {
    updateRoleDraft(updateRolePermissionState(form, [permission], allowed));
    onErrorsChange({ ...errors, permissions: undefined });
  };
  const setPermissionGroup = (permissionsToChange: string[], allowed: boolean) => {
    updateRoleDraft(updateRolePermissionState(form, permissionsToChange, allowed));
    onErrorsChange({ ...errors, permissions: undefined });
  };

  return (
    <section className="role-permission-workspace">
      <aside className="role-picker" aria-label="Roles">
        <div className="role-picker-heading"><div><p className="eyebrow">Roles</p><h3>Access bundles</h3></div></div>
        <Input aria-label="Search roles" placeholder="Search roles" value={roleSearch} onChange={(event) => setRoleSearch(event.target.value)} />
        <div className="role-picker-list">
          {visibleRoles.map((role) => (
            <div key={role.id} className={form.id === role.id ? "role-picker-item role-picker-item-active" : "role-picker-item"}>
              <button className="role-picker-select" type="button" onClick={() => { setIsSaveConfirmationOpen(false); onFormChange(roleDrafts[role.id] ?? toRoleForm(role)); onErrorsChange({}); }}>
                <span>{role.name}</span>
                <small>{role.isSystem ? "System" : "Custom"}</small>
              </button>
            </div>
          ))}
          {visibleRoles.length === 0 && roleSearch.trim() ? <p className="muted-copy role-picker-empty">No matching roles.</p> : null}
        </div>
      </aside>
      <div className="permission-matrix">
        <div className="role-editor-heading">
          <div><p className="eyebrow">Selected role</p><h3>{selectedRole?.name ?? "Choose a role"}</h3></div>
          <div className="role-editor-actions">
            {selectedRole ? <Input aria-label="Search permissions" placeholder="Search permissions" value={permissionSearch} onChange={(event) => setPermissionSearch(event.target.value)} /> : null}
            {selectedRole && hasPermissionChanges ? <Badge variant="secondary">{dirtyRoleDrafts.length} role{dirtyRoleDrafts.length === 1 ? "" : "s"} changed</Badge> : null}
            {selectedRole ? <Button disabled={!hasPermissionChanges || isSaving} onClick={() => setIsSaveConfirmationOpen(true)}>{isSaving ? "Saving..." : "Save changes"}</Button> : null}
          </div>
        </div>
        {errors.permissions ? <span className="field-error">{errors.permissions}</span> : null}
        {selectedRole ? <div className="permission-group-list">
          {visiblePermissionGroups.map((group) => {
            const allAllowed = group.permissions.every((permission) => form.permissions.includes(permission));
            const isExpanded = normalizedPermissionSearch ? true : expandedPermissionGroups[group.id] ?? true;
            const groupContentId = `permission-group-${group.id}`;
            return <section className="permission-group-card" key={group.id}>
              <div className="permission-row permission-group-row">
                <button
                  className="permission-group-toggle"
                  type="button"
                  aria-expanded={isExpanded}
                  aria-controls={groupContentId}
                  onClick={() => setExpandedPermissionGroups((current) => ({ ...current, [group.id]: !isExpanded }))}
                >
                  <ChevronIcon expanded={isExpanded} />
                  <span>
                    <strong>{group.name}</strong>
                    <small>{group.permissions.length} permissions</small>
                  </span>
                </button>
                <PermissionDecision
                  allowed={allAllowed}
                  label={`${allAllowed ? "Disable" : "Enable"} all ${group.name} permissions`}
                  onToggle={() => setPermissionGroup(group.permissions, !allAllowed)}
                />
              </div>
              <div
                id={groupContentId}
                className={isExpanded ? "permission-group-content permission-group-content-expanded" : "permission-group-content"}
                aria-hidden={!isExpanded}
                inert={!isExpanded}
                style={{ "--permission-group-height": `${group.permissions.length * 72}px` } as CSSProperties}
              >
                <div className="permission-group-items">
                  {group.permissions.map((permission) => (
                    <div className="permission-row" key={permission}>
                      <div className="permission-copy">
                        <span>{getPermissionDescription(permission)}</span>
                        <code>{permission}</code>
                      </div>
                      <PermissionDecision
                        allowed={form.permissions.includes(permission)}
                        label={`${form.permissions.includes(permission) ? "Disable" : "Enable"} ${permission}`}
                        onToggle={() => setPermission(permission, !form.permissions.includes(permission))}
                      />
                    </div>
                  ))}
                </div>
              </div>
            </section>;
          })}
          {selectedRole && visiblePermissionGroups.length === 0 ? <p className="muted-copy permission-search-empty">No matching permissions.</p> : null}
        </div> : null}
      </div>
      <ConfirmDialog
        open={isSaveConfirmationOpen}
        title="Save permission changes?"
        description={`Apply all staged permission changes to ${dirtyRoleDrafts.length} role${dirtyRoleDrafts.length === 1 ? "" : "s"} in one update.`}
        confirmLabel="Save changes"
        onConfirm={() => {
          void onSave(dirtyRoleDrafts).then((succeeded) => {
            if (succeeded) setRoleDrafts({});
          });
          setIsSaveConfirmationOpen(false);
        }}
        onCancel={() => setIsSaveConfirmationOpen(false)}
      />
    </section>
  );
}

function ChevronIcon({ expanded }: { expanded: boolean }) {
  return (
    <svg className={expanded ? "permission-chevron permission-chevron-expanded" : "permission-chevron"} viewBox="0 0 24 24" aria-hidden="true">
      <path d="m9 18 6-6-6-6" />
    </svg>
  );
}

function PermissionDecision({ allowed, label, onToggle }: {
  allowed: boolean;
  label: string;
  onToggle: () => void;
}) {
  return (
    <button
      type="button"
      role="switch"
      className={allowed ? "permission-switch permission-switch-active" : "permission-switch"}
      aria-checked={allowed}
      aria-label={label}
      title={allowed ? "Active" : "Inactive"}
      onClick={onToggle}
    >
      <span aria-hidden="true" />
    </button>
  );
}
