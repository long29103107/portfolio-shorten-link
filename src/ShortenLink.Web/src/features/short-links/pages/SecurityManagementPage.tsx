import { useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import { getAdminPermissionState, getStoredCurrentUser } from "../api/adminSecurity";
import type { SecuritySection } from "../types";
import { formatDateTime } from "../types";
import {
  defaultSecurityUserDiscovery,
  discoverSecurityUsers,
  paginateItems,
  type SecurityUserDiscovery
} from "../domain/securityDiscovery";
import { Badge } from "@/shared/components/ui/badge";
import { Button } from "@/shared/components/ui/button";
import { Card, CardContent } from "@/shared/components/ui/card";
import { EmptyState } from "@/shared/components/EmptyState";
import { Input } from "@/shared/components/ui/input";
import { DataTable } from "@/shared/components/DataTable";
import type { RecoveryNotice } from "@/shared/api/recovery";
import { DiscoverySelect } from "@/shared/components/DiscoverySelect";
import { RefreshButton } from "@/shared/components/RefreshButton";
import { RowActionsMenu } from "@/shared/components/RowActionsMenu";
import { Pagination } from "@/shared/components/Pagination";
import { useDebouncedCallback } from "@/shared/hooks/useDebouncedCallback";
import { useSecurityManagementData } from "../hooks/useSecurityManagementData";
import { useSecurityMutations } from "../hooks/useSecurityMutations";
import { SecurityManagementDialogs } from "../components/SecurityManagementDialogs";
import { RolePermissionMatrix } from "../components/RolePermissionMatrix";

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
