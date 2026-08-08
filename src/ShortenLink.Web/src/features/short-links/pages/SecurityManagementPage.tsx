import { useEffect, useMemo, useState } from "react";
import type { CSSProperties, ReactNode } from "react";
import { getAdminPermissionState, getStoredCurrentUser, shortLinkPermissions } from "../api/adminSecurity";
import type { SecurityRole, SecuritySection } from "../types";
import { formatDateTime } from "../types";
import type { CustomRoleFieldErrors } from "../domain/securityValidation";
import {
  defaultSecurityUserDiscovery,
  discoverPermissionGroups,
  discoverSecurityRoles,
  discoverSecurityUsers,
  paginateItems,
  type SecurityUserDiscovery
} from "../domain/securityDiscovery";
import { Badge } from "../../../shared/components/ui/badge";
import { Button } from "../../../shared/components/ui/button";
import { Card, CardContent } from "../../../shared/components/ui/card";
import { EmptyState } from "../../../shared/components/EmptyState";
import { Input } from "../../../shared/components/ui/input";
import { DataTable } from "../../../shared/components/DataTable";
import type { RecoveryNotice } from "../../../shared/api/recovery";
import { DiscoverySelect } from "../../../shared/components/DiscoverySelect";
import { ConfirmDialog } from "../../../shared/components/ConfirmDialog";
import { RefreshButton } from "../../../shared/components/RefreshButton";
import { RowActionsMenu } from "../../../shared/components/RowActionsMenu";
import { Pagination } from "../../../shared/components/Pagination";
import { getPermissionDescription } from "../domain/permissionCatalog";
import { useDebouncedCallback } from "../../../shared/hooks/useDebouncedCallback";
import { useSecurityManagementData } from "../hooks/useSecurityManagementData";
import { toRoleForm, useSecurityMutations, type RoleFormState } from "../hooks/useSecurityMutations";
import { SecurityManagementDialogs } from "../components/SecurityManagementDialogs";

const permissionOptions = Object.values(shortLinkPermissions);
const permissionGroups = [
  { id: "short-links", name: "Short links", permissions: permissionOptions.filter((permission) => permission.startsWith("short_links.")) },
  { id: "reporting", name: "Reporting and audit", permissions: permissionOptions.filter((permission) => permission === "analytics.read" || permission === "audit_logs.read") },
  { id: "security", name: "Security", permissions: permissionOptions.filter((permission) => permission.startsWith("security.")) }
];

export function SecurityManagementPage({ section, onDirtyChange }: { section: SecuritySection; onDirtyChange?: (isDirty: boolean) => void }) {
  const adminPermissions = getAdminPermissionState();
  const currentUser = getStoredCurrentUser();
  const [userDiscovery, setUserDiscovery] = useState<SecurityUserDiscovery>(defaultSecurityUserDiscovery);
  const [userSearch, setUserSearch] = useState(defaultSecurityUserDiscovery.search);
  const [userPage, setUserPage] = useState(1);
  const [userPageSize, setUserPageSize] = useState(10);
  const {
    isLoading,
    readFailure,
    users,
    setUsers,
    systemRoles,
    setSystemRoles,
    customRoles,
    setCustomRoles,
    loadSecurity
  } = useSecurityManagementData(adminPermissions.canManageSecurityAssignments);
  const {
    actionFailure,
    setActionFailure,
    selectedUser,
    userDialogMode,
    setUserDialogMode,
    userPendingDelete,
    setUserPendingDelete,
    selectedUserIds,
    setSelectedUserIds,
    isBulkDisablingUsers,
    isBulkDisableConfirmationOpen,
    setIsBulkDisableConfirmationOpen,
    isCreateUserOpen,
    setIsCreateUserOpen,
    createUserForm,
    setCreateUserForm,
    createUserErrors,
    setCreateUserErrors,
    resetPassword,
    setResetPassword,
    resetPasswordConfirm,
    setResetPasswordConfirm,
    profileEmail,
    setProfileEmail,
    profileDisplayName,
    setProfileDisplayName,
    profileError,
    setProfileError,
    resetPasswordError,
    setResetPasswordError,
    assignedRoleIds,
    setAssignedRoleIds,
    roleAssignmentError,
    setRoleAssignmentError,
    roleForm,
    setRoleForm,
    roleFieldErrors,
    setRoleFieldErrors,
    roleDialogMode,
    isSavingRole,
    setHasRoleDraftChanges,
    hasUnsavedSecurityChanges,
    selectedEnabledUsers,
    createUser,
    openUserDialog,
    closeCreateUserDialog,
    submitUserDialog,
    confirmUserDelete,
    confirmBulkDisableUsers,
    saveRolePermissionOverrides,
    openCreateRoleDialog,
    openEditRoleDialog,
    closeRoleDialog,
    submitRoleDialog,
    requestRoleDelete,
    confirmRoleDelete
  } = useSecurityMutations({
    section,
    users,
    setUsers,
    systemRoles,
    setSystemRoles,
    customRoles,
    setCustomRoles
  });

  const roleOptions = useMemo(
    () => [...systemRoles, ...customRoles].filter((role) => role.isEnabled),
    [customRoles, systemRoles]
  );
  const discoveredUsers = useMemo(() => discoverSecurityUsers(users, userDiscovery), [userDiscovery, users]);
  const userTotalPages = Math.max(1, Math.ceil(discoveredUsers.length / userPageSize));
  const visibleUsers = useMemo(
    () => paginateItems(discoveredUsers, Math.min(userPage, userTotalPages), userPageSize),
    [discoveredUsers, userPage, userPageSize, userTotalPages]
  );

  const updateUserDiscovery = (patch: Partial<SecurityUserDiscovery>) => {
    setUserDiscovery((current) => ({ ...current, ...patch }));
    setUserPage(1);
  };
  const debouncedUserSearch = useDebouncedCallback(
    (search: string) => updateUserDiscovery({ search: search.trim() }),
    400
  );

  useEffect(() => {
    onDirtyChange?.(hasUnsavedSecurityChanges);
  }, [hasUnsavedSecurityChanges, onDirtyChange]);

  useEffect(() => () => onDirtyChange?.(false), [onDirtyChange]);

  useEffect(() => {
    debouncedUserSearch.cancel();
    setUserSearch(userDiscovery.search);
  }, [userDiscovery.search]);

  if (!currentUser) {
    return <EmptyState title="Sign in required" description="Sign in to manage users and roles." />;
  }

  if (!adminPermissions.canManageSecurityAssignments) {
    return <EmptyState title="Admin role required" description="Only administrators can manage users and roles." />;
  }

  return (
    <>
      <nav className="page-breadcrumb-bar" aria-label="Breadcrumb">
        <ol className="page-breadcrumb">
          <li>Shorten Link</li>
          <li>Identity &amp; Access</li>
          <li aria-current="page">{section === "roles" ? "Roles & access controls" : "Users & access controls"}</li>
        </ol>
        <RefreshButton
          isRefreshing={isLoading}
          label="Refresh security data"
          onRefresh={loadSecurity}
        />
      </nav>
      <Card className="admin-panel security-management-panel">
        <CardContent>
        {readFailure ? <RecoveryBanner notice={readFailure} onRetry={() => void loadSecurity()} /> : null}
        {actionFailure ? <RecoveryBanner notice={actionFailure} onDismiss={() => setActionFailure(null)} /> : null}

        {section === "users" ? (
          <div className="security-tab-stack">
            <div className="security-list-header">
              <div>
                <p className="eyebrow">Users</p>
                <h3>Manage registered identities</h3>
              </div>
              <Button onClick={() => setIsCreateUserOpen(true)}>Create</Button>
            </div>

            <div className="admin-discovery-toolbar">
              <div className="admin-discovery-search"><Input
                aria-label="Search users"
                placeholder="Search email or display name"
                value={userSearch}
                onChange={(event) => {
                  setUserSearch(event.target.value);
                  debouncedUserSearch.invoke(event.target.value);
                }}
              /></div>
              <DiscoverySelect label="Status" value={userDiscovery.status} onChange={(status) => updateUserDiscovery({ status })}><option value="all">All</option><option value="enabled">Enabled</option><option value="disabled">Disabled</option></DiscoverySelect>
              <DiscoverySelect label="Role" value={userDiscovery.role} onChange={(role) => updateUserDiscovery({ role })}>
                <option value="all">All roles</option>
                <option value="none">No roles</option>
                {[...systemRoles, ...customRoles].map((role) => <option key={role.id} value={role.id}>{role.name}</option>)}
              </DiscoverySelect>
            </div>

            {visibleUsers.length === 0 ? (
              <EmptyState title={users.length === 0 ? "No users" : "No matching users"} description={users.length === 0 ? "Create a user to populate this table." : "Try different search or filter criteria."} />
            ) : (
              <DataTable
                ariaLabel="Managed users"
                rows={visibleUsers}
                getRowKey={(user) => user.id}
                bulkSelection={{
                  selectedKeys: selectedUserIds,
                  onChange: setSelectedUserIds,
                  getRowLabel: (user) => `Select ${user.username}`,
                  clearDisabled: isBulkDisablingUsers,
                  actions: selectedEnabledUsers.length > 0 ? [{
                    id: "disable",
                    label: isBulkDisablingUsers ? "Disabling..." : `Disable selected (${selectedEnabledUsers.length})`,
                    variant: "destructive",
                    disabled: isBulkDisablingUsers,
                    onSelect: () => setIsBulkDisableConfirmationOpen(true)
                  }] : []
                }}
                columns={[
                  { id: "email", header: "Email", cell: (user) => <button type="button" className="table-link-button" onClick={() => openUserDialog(user, "edit")}>{user.username}</button> },
                  { id: "displayName", header: "Display name", cell: (user) => user.displayName },
                  { id: "roles", header: "Roles", cell: (user) => user.roleIds.join(", ") || "No roles" },
                  { id: "created", header: "Created", cell: (user) => formatDateTime(user.createdAtUtc) },
                  { id: "status", header: "Status", cell: (user) => <Badge variant={user.isEnabled ? "default" : "destructive"}>{user.isEnabled ? "Enabled" : "Disabled"}</Badge> },
                  { id: "actions", header: "Actions", cell: (user) => <RowActionsMenu label={`Actions for ${user.username}`} actions={[
                    { id: "edit", label: "Edit user", onSelect: () => openUserDialog(user, "edit") },
                    { id: "password", label: "Set password", onSelect: () => openUserDialog(user, "password") },
                    { id: "roles", label: "Assign roles", onSelect: () => openUserDialog(user, "roles") },
                    ...(user.isEnabled ? [{ id: "delete", label: "Delete user", destructive: true, onSelect: () => setUserPendingDelete(user) }] : [])
                  ]} /> }
                ]}
              />
            )}

            {discoveredUsers.length > 0 ? (
              <Pagination
                ariaLabel="User pagination"
                totalItems={discoveredUsers.length}
                page={userPage}
                totalPages={userTotalPages}
                pageSize={userPageSize}
                pageSizeOptions={[10, 25, 50]}
                onPageChange={setUserPage}
                onPageSizeChange={(pageSize) => {
                  setUserPageSize(pageSize);
                  setUserPage(1);
                }}
              />
            ) : null}

          </div>
        ) : null}

        {section === "roles" ? (
          isLoading && systemRoles.length + customRoles.length === 0 ? (
            <EmptyState title="Loading roles" description="Loading role definitions and permission assignments." />
          ) : <RolePermissionMatrix
            roles={[...systemRoles, ...customRoles]}
            form={roleForm}
            errors={roleFieldErrors}
            onFormChange={setRoleForm}
            onErrorsChange={setRoleFieldErrors}
            isSaving={isSavingRole}
            onDirtyChange={setHasRoleDraftChanges}
            onSave={saveRolePermissionOverrides}
          />
        ) : null}

        </CardContent>
        <SecurityManagementDialogs
          isCreateUserOpen={isCreateUserOpen}
          createUserForm={createUserForm}
          setCreateUserForm={setCreateUserForm}
          createUserErrors={createUserErrors}
          setCreateUserErrors={setCreateUserErrors}
          createUser={createUser}
          closeCreateUserDialog={closeCreateUserDialog}
          isBulkDisableConfirmationOpen={isBulkDisableConfirmationOpen}
          selectedEnabledUserCount={selectedEnabledUsers.length}
          setIsBulkDisableConfirmationOpen={setIsBulkDisableConfirmationOpen}
          confirmBulkDisableUsers={confirmBulkDisableUsers}
          userPendingDelete={userPendingDelete}
          setUserPendingDelete={setUserPendingDelete}
          confirmUserDelete={confirmUserDelete}
          userDialogMode={userDialogMode}
          setUserDialogMode={setUserDialogMode}
          selectedUser={selectedUser}
          submitUserDialog={submitUserDialog}
          profileEmail={profileEmail}
          setProfileEmail={setProfileEmail}
          profileDisplayName={profileDisplayName}
          setProfileDisplayName={setProfileDisplayName}
          profileError={profileError}
          setProfileError={setProfileError}
          resetPassword={resetPassword}
          setResetPassword={setResetPassword}
          resetPasswordConfirm={resetPasswordConfirm}
          setResetPasswordConfirm={setResetPasswordConfirm}
          resetPasswordError={resetPasswordError}
          setResetPasswordError={setResetPasswordError}
          roleOptions={roleOptions}
          assignedRoleIds={assignedRoleIds}
          setAssignedRoleIds={setAssignedRoleIds}
          roleAssignmentError={roleAssignmentError}
          setRoleAssignmentError={setRoleAssignmentError}
        />
      </Card>
    </>
  );
}

function RolePermissionMatrix({ roles, form, errors, isSaving, onDirtyChange, onFormChange, onErrorsChange, onSave }: {
  roles: SecurityRole[];
  form: RoleFormState;
  errors: CustomRoleFieldErrors;
  isSaving: boolean;
  onDirtyChange: (isDirty: boolean) => void;
  onFormChange: (form: RoleFormState) => void;
  onErrorsChange: (errors: CustomRoleFieldErrors) => void;
  onSave: (drafts: RoleFormState[]) => Promise<boolean>;
}) {
  const selectedRole = roles.find((role) => role.id === form.id);
  const [roleSearch, setRoleSearch] = useState("");
  const [permissionSearch, setPermissionSearch] = useState("");
  const [expandedPermissionGroups, setExpandedPermissionGroups] = useState<Record<string, boolean>>(
    () => Object.fromEntries(permissionGroups.map((group) => [group.id, true]))
  );
  const [isSaveConfirmationOpen, setIsSaveConfirmationOpen] = useState(false);
  const [roleDrafts, setRoleDrafts] = useState<Record<string, RoleFormState>>({});
  const persistedOverrides = Object.fromEntries(
    (selectedRole?.permissionOverrides ?? []).map((item) => [item.permission, item.isAllowed])
  );
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
    const defaultAllowed = form.defaultPermissions.includes(permission);
    const permissionOverrides = { ...form.permissionOverrides };
    if (allowed === defaultAllowed) delete permissionOverrides[permission];
    else permissionOverrides[permission] = allowed;
    const permissions = allowed
      ? Array.from(new Set([...form.permissions, permission]))
      : form.permissions.filter((value) => value !== permission);
    updateRoleDraft({ ...form, permissions, permissionOverrides });
    onErrorsChange({ ...errors, permissions: undefined });
  };
  const setPermissionGroup = (permissionsToChange: string[], allowed: boolean) => {
    const permissionSet = new Set(form.permissions);
    const permissionOverrides = { ...form.permissionOverrides };

    permissionsToChange.forEach((permission) => {
      const defaultAllowed = form.defaultPermissions.includes(permission);
      if (allowed) permissionSet.add(permission);
      else permissionSet.delete(permission);
      if (allowed === defaultAllowed) delete permissionOverrides[permission];
      else permissionOverrides[permission] = allowed;
    });

    updateRoleDraft({ ...form, permissions: Array.from(permissionSet), permissionOverrides });
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

function EditIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M4 20h4l10.8-10.8a2.8 2.8 0 0 0-4-4L4 16v4Z" />
      <path d="m13.5 6.5 4 4" />
    </svg>
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

function RecoveryBanner({ notice, onRetry, onDismiss }: { notice: RecoveryNotice; onRetry?: () => void; onDismiss?: () => void }) {
  return (
    <div className="recovery-banner recovery-banner-error" role="alert">
      <span>{notice.message}{notice.retryable ? " Your current form values are still available." : ""}</span>
      {notice.retryable && onRetry ? <Button variant="secondary" onClick={onRetry}>Retry</Button> : null}
      {onDismiss ? <Button variant="ghost" onClick={onDismiss}>Dismiss</Button> : null}
    </div>
  );
}

function SecurityItem({ title, enabled, badge, children }: { title: string; enabled: boolean; badge?: string; children: ReactNode }) {
  return (
    <div className="security-assignment-item">
      <div className="security-assignment-item-header"><strong>{title}</strong><div className="security-badge-row">{badge ? <Badge variant="secondary">{badge}</Badge> : null}<Badge variant={enabled ? "default" : "destructive"}>{enabled ? "Enabled" : "Disabled"}</Badge></div></div>
      {children}
    </div>
  );
}
