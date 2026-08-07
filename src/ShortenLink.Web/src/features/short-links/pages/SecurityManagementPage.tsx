import { useEffect, useMemo, useState } from "react";
import type { CSSProperties, ReactNode } from "react";
import {
  deleteCustomSecurityRole,
  disableSecurityUser,
  replaceSecurityRolePermissionOverrides,
  upsertCustomSecurityRole,
  upsertSecurityUser
} from "../api/shortLinksApi";
import { getAdminPermissionState, getStoredCurrentUser, shortLinkPermissions } from "../api/adminSecurity";
import { ApiError } from "../api/http";
import type { SecurityRole, SecuritySection, SecurityUser } from "../types";
import { formatDateTime, toFriendlyErrorMessage } from "../types";
import {
  hasFieldErrors,
  mapManagedUserApiFieldErrors,
  mapPasswordResetApiFieldErrors,
  mapRoleAssignmentApiFieldErrors,
  validateManagedUserForm,
  validatePasswordReset,
  type ManagedUserFieldErrors
} from "../identityValidation";
import {
  mapCustomRoleApiFieldErrors,
  validateCustomRoleForm,
  type CustomRoleFieldErrors
} from "../securityValidation";
import {
  defaultSecurityUserDiscovery,
  discoverPermissionGroups,
  discoverSecurityRoles,
  discoverSecurityUsers,
  paginateItems,
  type SecurityUserDiscovery
} from "../securityDiscovery";
import { Badge } from "../../../shared/components/ui/badge";
import { Button } from "../../../shared/components/ui/button";
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from "../../../shared/components/ui/card";
import { EmptyState } from "../../../shared/components/EmptyState";
import { Input } from "../../../shared/components/ui/input";
import { Label } from "../../../shared/components/ui/label";
import { DataTable } from "../../../shared/components/DataTable";
import { showToast } from "../../../shared/toast";
import { createRecoveryNotice, type RecoveryNotice } from "../../../shared/api/recovery";
import { FormField } from "../../../shared/components/FormField";
import { DiscoverySelect } from "../../../shared/components/DiscoverySelect";
import { ConfirmDialog } from "../../../shared/components/ConfirmDialog";
import { FormDialog } from "../../../shared/components/FormDialog";
import { RefreshButton } from "../../../shared/components/RefreshButton";
import { RowActionsMenu } from "../../../shared/components/RowActionsMenu";
import { Pagination } from "../../../shared/components/Pagination";
import { getPermissionDescription } from "../permissionCatalog";
import { useDebouncedCallback } from "../../../shared/hooks/useDebouncedCallback";
import { useSecurityManagementData } from "../hooks/useSecurityManagementData";

const permissionOptions = Object.values(shortLinkPermissions);
const permissionGroups = [
  { id: "short-links", name: "Short links", permissions: permissionOptions.filter((permission) => permission.startsWith("short_links.")) },
  { id: "reporting", name: "Reporting and audit", permissions: permissionOptions.filter((permission) => permission === "analytics.read" || permission === "audit_logs.read") },
  { id: "security", name: "Security", permissions: permissionOptions.filter((permission) => permission.startsWith("security.")) }
];

type RoleFormState = {
  id: string;
  name: string;
  permissions: string[];
  defaultPermissions: string[];
  permissionOverrides: Record<string, boolean>;
  isEnabled: boolean;
};

const emptyRoleForm: RoleFormState = {
  id: "",
  name: "",
  permissions: [],
  defaultPermissions: [],
  permissionOverrides: {},
  isEnabled: true
};

function toRoleForm(role: SecurityRole): RoleFormState {
  return {
    id: role.id,
    name: role.name,
    permissions: role.permissions,
    defaultPermissions: role.defaultPermissions,
    permissionOverrides: Object.fromEntries(role.permissionOverrides.map((item) => [item.permission, item.isAllowed])),
    isEnabled: role.isEnabled
  };
}

export function SecurityManagementPage({ section, onDirtyChange }: { section: SecuritySection; onDirtyChange?: (isDirty: boolean) => void }) {
  const adminPermissions = getAdminPermissionState();
  const currentUser = getStoredCurrentUser();
  const [actionFailure, setActionFailure] = useState<RecoveryNotice | null>(null);
  const [selectedUserId, setSelectedUserId] = useState<string | null>(null);
  const [userDialogMode, setUserDialogMode] = useState<"edit" | "password" | "roles" | null>(null);
  const [userPendingDelete, setUserPendingDelete] = useState<SecurityUser | null>(null);
  const [selectedUserIds, setSelectedUserIds] = useState<Set<string>>(() => new Set());
  const [isBulkDisablingUsers, setIsBulkDisablingUsers] = useState(false);
  const [isBulkDisableConfirmationOpen, setIsBulkDisableConfirmationOpen] = useState(false);
  const [isCreateUserOpen, setIsCreateUserOpen] = useState(false);
  const [userDiscovery, setUserDiscovery] = useState<SecurityUserDiscovery>(defaultSecurityUserDiscovery);
  const [userSearch, setUserSearch] = useState(defaultSecurityUserDiscovery.search);
  const [userPage, setUserPage] = useState(1);
  const [userPageSize, setUserPageSize] = useState(10);
  const [createUserForm, setCreateUserForm] = useState({ email: "", displayName: "", password: "" });
  const [createUserErrors, setCreateUserErrors] = useState<ManagedUserFieldErrors>({});
  const [resetPassword, setResetPassword] = useState("");
  const [resetPasswordConfirm, setResetPasswordConfirm] = useState("");
  const [profileEmail, setProfileEmail] = useState("");
  const [profileDisplayName, setProfileDisplayName] = useState("");
  const [profileError, setProfileError] = useState<string | undefined>();
  const [resetPasswordError, setResetPasswordError] = useState<string | undefined>();
  const [assignedRoleIds, setAssignedRoleIds] = useState<string[]>([]);
  const [roleAssignmentError, setRoleAssignmentError] = useState<string | undefined>();
  const [roleForm, setRoleForm] = useState<RoleFormState>(emptyRoleForm);
  const [roleFieldErrors, setRoleFieldErrors] = useState<CustomRoleFieldErrors>({});
  const [rolePendingDelete, setRolePendingDelete] = useState<SecurityRole | null>(null);
  const [roleDialogMode, setRoleDialogMode] = useState<"create" | "edit" | null>(null);
  const [isSavingRole, setIsSavingRole] = useState(false);
  const [roleFormBeforeDialog, setRoleFormBeforeDialog] = useState<typeof roleForm | null>(null);
  const [hasRoleDraftChanges, setHasRoleDraftChanges] = useState(false);
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

  const selectedUser = useMemo(
    () => users.find((user) => user.id === selectedUserId) ?? null,
    [selectedUserId, users]
  );
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
  const hasUserDialogChanges = isCreateUserOpen
    ? Boolean(createUserForm.email || createUserForm.displayName || createUserForm.password)
    : userDialogMode === "edit" && selectedUser
      ? profileEmail !== selectedUser.username || profileDisplayName !== selectedUser.displayName
      : userDialogMode === "password"
        ? Boolean(resetPassword || resetPasswordConfirm)
        : userDialogMode === "roles" && selectedUser
          ? [...assignedRoleIds].sort().join("|") !== [...selectedUser.roleIds].sort().join("|")
          : false;
  const hasUnsavedSecurityChanges = hasRoleDraftChanges || hasUserDialogChanges;

  useEffect(() => {
    onDirtyChange?.(hasUnsavedSecurityChanges);
  }, [hasUnsavedSecurityChanges, onDirtyChange]);

  useEffect(() => () => onDirtyChange?.(false), [onDirtyChange]);

  useEffect(() => {
    debouncedUserSearch.cancel();
    setUserSearch(userDiscovery.search);
  }, [userDiscovery.search]);

  useEffect(() => {
    if (section !== "roles") return;
    const roles = [...systemRoles, ...customRoles];
    if (roles.length === 0) {
      if (roleForm.id) setRoleForm(emptyRoleForm);
      return;
    }
    if (!roles.some((role) => role.id === roleForm.id)) {
      setRoleForm(toRoleForm(roles[0]));
      setRoleFieldErrors({});
    }
  }, [section, systemRoles, customRoles, roleForm.id]);

  useEffect(() => {
    if (section === "roles") {
      setIsCreateUserOpen(false);
      setUserDialogMode(null);
      setUserPendingDelete(null);
      setCreateUserForm({ email: "", displayName: "", password: "" });
      setCreateUserErrors({});
      setResetPassword("");
      setResetPasswordConfirm("");
      setHasRoleDraftChanges(false);
      return;
    }

    const persistedRole = [...systemRoles, ...customRoles].find((role) => role.id === roleForm.id);
    setRoleForm(persistedRole ? toRoleForm(persistedRole) : emptyRoleForm);
    setRoleFieldErrors({});
    setHasRoleDraftChanges(false);
  }, [section]);

  const createUser = async () => {
    const errors = validateManagedUserForm(createUserForm);
    if (hasFieldErrors(errors)) {
      setCreateUserErrors(errors);
      setActionFailure(null);
      return;
    }

    setCreateUserErrors({});
    setActionFailure(null);
    try {
      const user = await upsertSecurityUser({
        id: createInternalUserId(),
        username: createUserForm.email.trim(),
        displayName: createUserForm.displayName.trim(),
        password: null,
        roleIds: ["User"],
        isEnabled: true
      });
      setUsers((current) => upsertBy(current, user, "id"));
      setCreateUserForm({ email: "", displayName: "", password: "" });
      setIsCreateUserOpen(false);
      selectUser(user);
      showToast({ title: "User registered", message: user.username, variant: "success" });
    } catch (error) {
      const fieldErrors = error instanceof ApiError ? mapManagedUserApiFieldErrors(error.fieldErrors) : {};
      setCreateUserErrors(fieldErrors);
      setActionFailure(hasFieldErrors(fieldErrors) ? null : toRecoveryNotice(error, "User could not be registered."));
    }
  };

  const selectUser = (user: SecurityUser) => {
    setSelectedUserId(user.id);
    setAssignedRoleIds(user.roleIds);
    setProfileEmail(user.username);
    setProfileDisplayName(user.displayName);
    setProfileError(undefined);
    setResetPassword("");
    setResetPasswordConfirm("");
    setResetPasswordError(undefined);
    setRoleAssignmentError(undefined);
    setActionFailure(null);
  };

  const openUserDialog = (user: SecurityUser, mode: "edit" | "password" | "roles") => {
    selectUser(user);
    setUserDialogMode(mode);
  };

  const updateSelectedUserProfile = async () => {
    if (!selectedUser) return;
    if (!profileEmail.trim() || !profileDisplayName.trim()) {
      setProfileError("Enter email and display name.");
      return;
    }
    setProfileError(undefined);
    try {
      const updated = await upsertSecurityUser({ ...selectedUser, username: profileEmail.trim(), displayName: profileDisplayName.trim(), password: null });
      setUsers((current) => upsertBy(current, updated, "id"));
      setUserDialogMode(null);
      showToast({ title: "User updated", message: updated.username, variant: "success" });
    } catch (error) {
      setActionFailure(toRecoveryNotice(error, "User could not be updated."));
    }
  };

  const resetSelectedUserPassword = async () => {
    if (!selectedUser) return;
    const errors = validatePasswordReset(resetPassword);
    if (errors.password) {
      setResetPasswordError(errors.password);
      return;
    }
    if (resetPassword !== resetPasswordConfirm) {
      setResetPasswordError("Passwords do not match.");
      return;
    }

    setResetPasswordError(undefined);
    setActionFailure(null);
    try {
      const updated = await upsertSecurityUser({
        id: selectedUser.id,
        username: selectedUser.username,
        displayName: selectedUser.displayName,
        password: resetPassword,
        roleIds: selectedUser.roleIds,
        isEnabled: selectedUser.isEnabled
      });
      setUsers((current) => upsertBy(current, updated, "id"));
      setResetPassword("");
      setResetPasswordConfirm("");
      setUserDialogMode(null);
      showToast({ title: "Password reset", message: selectedUser.username, variant: "success" });
    } catch (error) {
      const fieldErrors = error instanceof ApiError ? mapPasswordResetApiFieldErrors(error.fieldErrors) : {};
      setResetPasswordError(fieldErrors.password);
      setActionFailure(fieldErrors.password ? null : toRecoveryNotice(error, "Password could not be reset."));
    }
  };

  const saveSelectedUserRoles = async () => {
    if (!selectedUser) return;
    setRoleAssignmentError(undefined);
    setActionFailure(null);
    try {
      const updated = await upsertSecurityUser({
        id: selectedUser.id,
        username: selectedUser.username,
        displayName: selectedUser.displayName,
        password: null,
        roleIds: assignedRoleIds,
        isEnabled: selectedUser.isEnabled
      });
      setUsers((current) => upsertBy(current, updated, "id"));
      setUserDialogMode(null);
      showToast({ title: "Roles assigned", message: selectedUser.username, variant: "success" });
    } catch (error) {
      const fieldErrors = error instanceof ApiError ? mapRoleAssignmentApiFieldErrors(error.fieldErrors) : {};
      setRoleAssignmentError(fieldErrors.roleIds);
      setActionFailure(fieldErrors.roleIds ? null : toRecoveryNotice(error, "Roles could not be assigned."));
    }
  };

  const saveRole = async () => {
    const errors = validateCustomRoleForm(roleForm);
    if (hasFieldErrors(errors)) {
      setRoleFieldErrors(errors);
      setActionFailure(null);
      return false;
    }

    setRoleFieldErrors({});
    setActionFailure(null);
    setIsSavingRole(true);
    try {
      const role = await upsertCustomSecurityRole({
        id: roleForm.id.trim(),
        name: roleForm.name.trim(),
        permissions: roleForm.defaultPermissions,
        isEnabled: roleForm.isEnabled
      });
      setCustomRoles((current) => upsertBy(current, role, "id"));
      setRoleForm(toRoleForm(role));
      showToast({ title: "Role saved", message: role.name, variant: "success" });
      return true;
    } catch (error) {
      const fieldErrors = error instanceof ApiError ? mapCustomRoleApiFieldErrors(error.fieldErrors) : {};
      setRoleFieldErrors(fieldErrors);
      setActionFailure(hasFieldErrors(fieldErrors) ? null : toRecoveryNotice(error, "Role could not be saved."));
      return false;
    } finally {
      setIsSavingRole(false);
    }
  };

  const closeCreateUserDialog = () => {
    setCreateUserForm({ email: "", displayName: "", password: "" });
    setCreateUserErrors({});
    setIsCreateUserOpen(false);
  };

  const deactivateUser = async (user: SecurityUser) => {
    try {
      const result = await disableSecurityUser(user.id);
      setUsers((current) => current.map((item) => item.id === result.id ? { ...item, isEnabled: false } : item));
      showToast({ title: "User disabled", message: user.username, variant: "success" });
    } catch (error) {
      setActionFailure(toRecoveryNotice(error, "User could not be disabled."));
    }
  };

  const submitUserDialog = () => {
    if (userDialogMode === "edit") void updateSelectedUserProfile();
    if (userDialogMode === "password") void resetSelectedUserPassword();
    if (userDialogMode === "roles") void saveSelectedUserRoles();
  };

  const confirmUserDelete = async () => {
    if (!userPendingDelete) return;
    await deactivateUser(userPendingDelete);
    setUserPendingDelete(null);
  };

  const selectedEnabledUsers = users.filter((user) => selectedUserIds.has(user.id) && user.isEnabled);

  const confirmBulkDisableUsers = async () => {
    if (selectedEnabledUsers.length === 0) return;
    setIsBulkDisablingUsers(true);
    setActionFailure(null);
    try {
      const results = await Promise.all(selectedEnabledUsers.map((user) => disableSecurityUser(user.id)));
      const disabledIds = new Set(results.map((result) => result.id));
      setUsers((current) => current.map((user) => disabledIds.has(user.id) ? { ...user, isEnabled: false } : user));
      setSelectedUserIds(new Set());
      setIsBulkDisableConfirmationOpen(false);
      showToast({ title: "Users disabled", message: `${results.length} user${results.length === 1 ? "" : "s"} disabled.`, variant: "success" });
    } catch (error) {
      setActionFailure(toRecoveryNotice(error, "Selected users could not be disabled."));
    } finally {
      setIsBulkDisablingUsers(false);
    }
  };

  const saveRolePermissionOverrides = async (drafts: RoleFormState[]) => {
    if (drafts.length === 0) return false;
    setActionFailure(null);
    setIsSavingRole(true);
    try {
      const savedRoles = await Promise.all(drafts.map((draft) =>
        replaceSecurityRolePermissionOverrides(draft.id, {
          overrides: Object.entries(draft.permissionOverrides).map(([permission, isAllowed]) => ({ permission, isAllowed }))
        })
      ));
      savedRoles.forEach((role) => {
        if (!role.isSystem) setCustomRoles((current) => upsertBy(current, role, "id"));
        else setSystemRoles((current) => upsertBy(current, role, "id"));
      });
      const selectedSavedRole = savedRoles.find((role) => role.id === roleForm.id);
      if (selectedSavedRole) setRoleForm(toRoleForm(selectedSavedRole));
      showToast({ title: "Permission changes saved", message: `${savedRoles.length} role${savedRoles.length === 1 ? "" : "s"} updated.`, variant: "success" });
      return true;
    } catch (error) {
      setActionFailure(toRecoveryNotice(error, "Permission overrides could not be saved."));
      return false;
    } finally {
      setIsSavingRole(false);
    }
  };

  const openCreateRoleDialog = () => {
    setRoleFormBeforeDialog(roleForm);
    setRoleForm(emptyRoleForm);
    setRoleFieldErrors({});
    setActionFailure(null);
    setRoleDialogMode("create");
  };

  const openEditRoleDialog = (role: SecurityRole) => {
    if (role.isSystem) return;
    setRoleFormBeforeDialog(roleForm);
    setRoleForm(toRoleForm(role));
    setRoleFieldErrors({});
    setActionFailure(null);
    setRoleDialogMode("edit");
  };

  const closeRoleDialog = () => {
    setRoleDialogMode(null);
    if (roleFormBeforeDialog) setRoleForm(roleFormBeforeDialog);
    setRoleFormBeforeDialog(null);
    setRoleFieldErrors({});
  };

  const submitRoleDialog = async () => {
    if (await saveRole()) {
      setRoleDialogMode(null);
      setRoleFormBeforeDialog(null);
    }
  };

  const requestRoleDelete = (role: SecurityRole) => {
    const assignedUserCount = users.filter((user) =>
      user.roleIds.some((roleId) => roleId.toLowerCase() === role.id.toLowerCase())
    ).length;
    if (assignedUserCount > 0) {
      setActionFailure({
        message: `${role.name} is assigned to ${assignedUserCount} user(s). Remove or replace this role on those users before deleting it.`,
        retryable: false
      });
      return;
    }

    setActionFailure(null);
    setRolePendingDelete(role);
  };

  const confirmRoleDelete = async () => {
    if (!rolePendingDelete) return;
    const role = rolePendingDelete;
    setRolePendingDelete(null);
    setActionFailure(null);
    try {
      const result = await deleteCustomSecurityRole(role.id);
      setCustomRoles((current) => current.filter((item) => item.id !== result.id));
      if (roleForm.id === result.id) {
        setRoleForm(emptyRoleForm);
        setRoleFieldErrors({});
      }
      showToast({ title: "Role deleted", message: role.name, variant: "success" });
    } catch (error) {
      setActionFailure(toRecoveryNotice(error, "Role could not be deleted."));
    }
  };

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
        <FormDialog
          open={isCreateUserOpen}
          title="Create managed user"
          description="Create the identity first, then set its password and assign roles from the user actions menu."
          submitLabel="Create"
          onSubmit={() => void createUser()}
          onCancel={closeCreateUserDialog}
        >
          <div className="form-dialog-grid">
            <IdentityField id="new-user-email" label="Email" type="email" autoComplete="email" value={createUserForm.email} error={createUserErrors.email} onChange={(email) => {
              setCreateUserForm((current) => ({ ...current, email }));
              setCreateUserErrors((current) => ({ ...current, email: undefined }));
            }} />
            <IdentityField id="new-user-display-name" label="Display name" value={createUserForm.displayName} error={createUserErrors.displayName} onChange={(displayName) => {
              setCreateUserForm((current) => ({ ...current, displayName }));
              setCreateUserErrors((current) => ({ ...current, displayName: undefined }));
            }} />
          </div>
        </FormDialog>
        <ConfirmDialog
          open={isBulkDisableConfirmationOpen}
          title="Disable selected users?"
          description={`This disables sign-in for ${selectedEnabledUsers.length} selected user${selectedEnabledUsers.length === 1 ? "" : "s"} while preserving audit history.`}
          confirmLabel="Disable selected"
          variant="destructive"
          onConfirm={() => void confirmBulkDisableUsers()}
          onCancel={() => setIsBulkDisableConfirmationOpen(false)}
        />
        <ConfirmDialog
          open={userPendingDelete !== null}
          title={`Delete ${userPendingDelete?.displayName ?? "user"}?`}
          description="This disables sign-in for the user while preserving their audit history."
          confirmLabel="Delete user"
          variant="destructive"
          onConfirm={() => void confirmUserDelete()}
          onCancel={() => setUserPendingDelete(null)}
        />
        <FormDialog
          open={userDialogMode !== null}
          title={userDialogMode === "edit" ? "Edit user" : userDialogMode === "password" ? "Set password" : "Assign roles"}
          description={selectedUser ? `${selectedUser.displayName} · ${selectedUser.username}` : undefined}
          submitLabel={userDialogMode === "edit" ? "Save changes" : userDialogMode === "password" ? "Set new password" : "Save roles"}
          onSubmit={submitUserDialog}
          onCancel={() => setUserDialogMode(null)}
        >
          {userDialogMode === "edit" ? (
            <div className="form-dialog-grid">
              <IdentityField id="update-user-email" label="Email" type="email" value={profileEmail} disabled onChange={(value) => { setProfileEmail(value); setProfileError(undefined); }} />
              <IdentityField id="update-user-display" label="Display name" value={profileDisplayName} error={profileError} onChange={(value) => { setProfileDisplayName(value); setProfileError(undefined); }} />
            </div>
          ) : null}
          {userDialogMode === "password" ? (
            <div className="form-dialog-grid">
              <IdentityField id="reset-user-password" label="New password" type="password" autoComplete="new-password" value={resetPassword} onChange={(password) => {
                setResetPassword(password);
                setResetPasswordError(undefined);
              }} />
              <IdentityField id="confirm-reset-user-password" label="Confirm new password" type="password" autoComplete="new-password" value={resetPasswordConfirm} error={resetPasswordError} onChange={(password) => {
                setResetPasswordConfirm(password);
                setResetPasswordError(undefined);
              }} />
            </div>
          ) : null}
          {userDialogMode === "roles" ? (
            <RoleChoiceGroup roles={roleOptions} selected={assignedRoleIds} error={roleAssignmentError} onToggle={(roleId) => {
              setAssignedRoleIds((current) => current.includes(roleId) ? current.filter((id) => id !== roleId) : [...current, roleId]);
              setRoleAssignmentError(undefined);
            }} />
          ) : null}
        </FormDialog>
      </Card>
    </>
  );
}

function IdentityField({ id, label, value, error, type = "text", autoComplete, disabled, onChange }: {
  id: string;
  label: string;
  value: string;
  error?: string;
  type?: "text" | "email" | "password";
  autoComplete?: string;
  disabled?: boolean;
  onChange: (value: string) => void;
}) {
  return <FormField id={id} label={label} value={value} error={error} type={type} autoComplete={autoComplete} disabled={disabled} onChange={onChange} />;
}

function RoleChoiceGroup({ roles, selected, error, onToggle }: { roles: SecurityRole[]; selected: string[]; error?: string; onToggle: (roleId: string) => void }) {
  return (
    <fieldset className="security-choice-group security-permission-grid" aria-invalid={error ? "true" : undefined}>
      <legend>Roles</legend>
      {roles.map((role) => <label className="security-choice" key={role.id}><input type="checkbox" checked={selected.includes(role.id)} onChange={() => onToggle(role.id)} /><span>{role.name}</span></label>)}
      {error ? <span className="field-error">{error}</span> : null}
    </fieldset>
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

function upsertBy<T extends Record<K, string>, K extends keyof T>(items: T[], nextItem: T, key: K): T[] {
  return [...items.filter((item) => item[key] !== nextItem[key]), nextItem].sort((left, right) => String(left[key]).localeCompare(String(right[key])));
}

function createInternalUserId(): string {
  return `user-${crypto.randomUUID()}`;
}

function toRecoveryNotice(error: unknown, fallbackMessage: string) {
  const message = error instanceof ApiError ? toFriendlyErrorMessage(error.errorCode, error.message) : fallbackMessage;
  return createRecoveryNotice(error, message);
}
