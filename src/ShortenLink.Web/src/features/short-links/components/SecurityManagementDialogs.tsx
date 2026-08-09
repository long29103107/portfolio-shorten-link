import type { Dispatch, SetStateAction } from "react";
import type { SecurityRole, SecurityUser } from "../types";
import type { ManagedUserFieldErrors } from "../domain/identityValidation";
import { ConfirmDialog } from "@/shared/components/ConfirmDialog";
import { FormDialog } from "@/shared/components/FormDialog";
import { FormField } from "@/shared/components/FormField";
import type { CreateUserForm } from "../hooks/useSecurityMutations";

type SecurityManagementDialogsProps = {
  isCreateUserOpen: boolean;
  createUserForm: CreateUserForm;
  setCreateUserForm: Dispatch<SetStateAction<CreateUserForm>>;
  createUserErrors: ManagedUserFieldErrors;
  setCreateUserErrors: Dispatch<SetStateAction<ManagedUserFieldErrors>>;
  createUser: () => Promise<void>;
  closeCreateUserDialog: () => void;
  isBulkDisableConfirmationOpen: boolean;
  selectedEnabledUserCount: number;
  setIsBulkDisableConfirmationOpen: (open: boolean) => void;
  confirmBulkDisableUsers: () => Promise<void>;
  userPendingDelete: SecurityUser | null;
  setUserPendingDelete: (user: SecurityUser | null) => void;
  confirmUserDelete: () => Promise<void>;
  userDialogMode: "edit" | "password" | "roles" | null;
  setUserDialogMode: (mode: "edit" | "password" | "roles" | null) => void;
  selectedUser: SecurityUser | null;
  submitUserDialog: () => void;
  profileEmail: string;
  setProfileEmail: (value: string) => void;
  profileDisplayName: string;
  setProfileDisplayName: (value: string) => void;
  profileError: string | undefined;
  setProfileError: (value: string | undefined) => void;
  resetPassword: string;
  setResetPassword: (value: string) => void;
  resetPasswordConfirm: string;
  setResetPasswordConfirm: (value: string) => void;
  resetPasswordError: string | undefined;
  setResetPasswordError: (value: string | undefined) => void;
  roleOptions: SecurityRole[];
  assignedRoleIds: string[];
  setAssignedRoleIds: Dispatch<SetStateAction<string[]>>;
  roleAssignmentError: string | undefined;
  setRoleAssignmentError: (value: string | undefined) => void;
};

export function SecurityManagementDialogs({
  isCreateUserOpen,
  createUserForm,
  setCreateUserForm,
  createUserErrors,
  setCreateUserErrors,
  createUser,
  closeCreateUserDialog,
  isBulkDisableConfirmationOpen,
  selectedEnabledUserCount,
  setIsBulkDisableConfirmationOpen,
  confirmBulkDisableUsers,
  userPendingDelete,
  setUserPendingDelete,
  confirmUserDelete,
  userDialogMode,
  setUserDialogMode,
  selectedUser,
  submitUserDialog,
  profileEmail,
  setProfileEmail,
  profileDisplayName,
  setProfileDisplayName,
  profileError,
  setProfileError,
  resetPassword,
  setResetPassword,
  resetPasswordConfirm,
  setResetPasswordConfirm,
  resetPasswordError,
  setResetPasswordError,
  roleOptions,
  assignedRoleIds,
  setAssignedRoleIds,
  roleAssignmentError,
  setRoleAssignmentError
}: SecurityManagementDialogsProps) {
  return (
    <>
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
        description={`This disables sign-in for ${selectedEnabledUserCount} selected user${selectedEnabledUserCount === 1 ? "" : "s"} while preserving audit history.`}
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
