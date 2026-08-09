import { useEffect, useMemo, useState, type Dispatch, type SetStateAction } from "react";
import {
  deleteCustomSecurityRole,
  disableSecurityUser,
  replaceSecurityRolePermissionOverrides,
  upsertCustomSecurityRole,
  upsertSecurityUser
} from "../api/shortLinksApi";
import { ApiError } from "../api/http";
import type { SecurityRole, SecuritySection, SecurityUser } from "../types";
import {
  hasFieldErrors,
  mapManagedUserApiFieldErrors,
  mapPasswordResetApiFieldErrors,
  mapRoleAssignmentApiFieldErrors,
  validateManagedUserForm,
  validatePasswordReset,
  type ManagedUserFieldErrors
} from "../domain/identityValidation";
import {
  mapCustomRoleApiFieldErrors,
  validateCustomRoleForm,
  type CustomRoleFieldErrors
} from "../domain/securityValidation";
import { createRecoveryNotice, type RecoveryNotice } from "@/shared/api/recovery";
import { showToast } from "@/shared/toast";
import { toFriendlyErrorMessage } from "../types";

export type RoleFormState = {
  id: string;
  name: string;
  permissions: string[];
  defaultPermissions: string[];
  permissionOverrides: Record<string, boolean>;
  isEnabled: boolean;
};

export type CreateUserForm = {
  email: string;
  displayName: string;
  password: string;
};

export const emptyRoleForm: RoleFormState = {
  id: "",
  name: "",
  permissions: [],
  defaultPermissions: [],
  permissionOverrides: {},
  isEnabled: true
};

export function toRoleForm(role: SecurityRole): RoleFormState {
  return {
    id: role.id,
    name: role.name,
    permissions: role.permissions,
    defaultPermissions: role.defaultPermissions,
    permissionOverrides: Object.fromEntries(role.permissionOverrides.map((item) => [item.permission, item.isAllowed])),
    isEnabled: role.isEnabled
  };
}

export function buildRolePermissionOverridesRequest(roleForm: RoleFormState) {
  return {
    overrides: Object.entries(roleForm.permissionOverrides).map(([permission, isAllowed]) => ({ permission, isAllowed }))
  };
}

type UseSecurityMutationsOptions = {
  section: SecuritySection;
  users: SecurityUser[];
  setUsers: Dispatch<SetStateAction<SecurityUser[]>>;
  systemRoles: SecurityRole[];
  setSystemRoles: Dispatch<SetStateAction<SecurityRole[]>>;
  customRoles: SecurityRole[];
  setCustomRoles: Dispatch<SetStateAction<SecurityRole[]>>;
};

export function useSecurityMutations({
  section,
  users,
  setUsers,
  systemRoles,
  setSystemRoles,
  customRoles,
  setCustomRoles
}: UseSecurityMutationsOptions) {
  const [actionFailure, setActionFailure] = useState<RecoveryNotice | null>(null);
  const [selectedUserId, setSelectedUserId] = useState<string | null>(null);
  const [userDialogMode, setUserDialogMode] = useState<"edit" | "password" | "roles" | null>(null);
  const [userPendingDelete, setUserPendingDelete] = useState<SecurityUser | null>(null);
  const [selectedUserIds, setSelectedUserIds] = useState<Set<string>>(() => new Set());
  const [isBulkDisablingUsers, setIsBulkDisablingUsers] = useState(false);
  const [isBulkDisableConfirmationOpen, setIsBulkDisableConfirmationOpen] = useState(false);
  const [isCreateUserOpen, setIsCreateUserOpen] = useState(false);
  const [createUserForm, setCreateUserForm] = useState<CreateUserForm>({ email: "", displayName: "", password: "" });
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
  const [roleFormBeforeDialog, setRoleFormBeforeDialog] = useState<RoleFormState | null>(null);
  const [hasRoleDraftChanges, setHasRoleDraftChanges] = useState(false);

  const selectedUser = useMemo(
    () => users.find((user) => user.id === selectedUserId) ?? null,
    [selectedUserId, users]
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
        replaceSecurityRolePermissionOverrides(draft.id, buildRolePermissionOverridesRequest(draft))
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

  return {
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
    rolePendingDelete,
    setRolePendingDelete,
    roleDialogMode,
    isSavingRole,
    hasRoleDraftChanges,
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
  };
}

function upsertBy<T extends Record<K, string>, K extends keyof T>(items: T[], nextItem: T, key: K): T[] {
  return [...items.filter((item) => item[key] !== nextItem[key]), nextItem].sort((left, right) => String(left[key]).localeCompare(String(right[key])));
}

function createInternalUserId(): string {
  return `user-${crypto.randomUUID()}`;
}

function toRecoveryNotice(error: unknown, fallbackMessage: string): RecoveryNotice {
  const message = error instanceof ApiError ? toFriendlyErrorMessage(error.errorCode, error.message) : fallbackMessage;
  return createRecoveryNotice(error, message);
}
