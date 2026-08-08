import { useState } from "react";
import { ApiError } from "../api/http";
import {
  disableSecurityAssignment,
  upsertSecurityAssignment
} from "../api/shortLinksApi";
import { getAdminPermissionState } from "../api/adminSecurity";
import type { SecurityAssignment } from "../types";
import { formatDateTime, toFriendlyErrorMessage } from "../types";
import { useSecurityAssignmentsData } from "../hooks/useSecurityAssignmentsData";
import { Badge } from "../../../shared/components/ui/badge";
import { Button } from "../../../shared/components/ui/button";
import { RefreshButton } from "../../../shared/components/RefreshButton";
import { Card, CardContent, CardHeader, CardTitle } from "../../../shared/components/ui/card";
import { ConfirmDialog } from "../../../shared/components/ConfirmDialog";
import { EmptyState } from "../../../shared/components/EmptyState";
import { FormField } from "../../../shared/components/FormField";
import { showToast } from "../../../shared/toast";

type SecurityAssignmentFieldErrors = {
  name?: string;
  credentialKey?: string;
};

type ConfirmAction = {
  title: string;
  description: string;
  confirmLabel: string;
  variant?: "default" | "destructive";
  onConfirm: () => void;
};

const systemRoleOptions = ["Admin", "User"] as const;

const permissionOptions = [
  "short_links.read",
  "short_links.create",
  "short_links.update",
  "short_links.status",
  "short_links.delete",
  "short_links.import",
  "analytics.read",
  "audit_logs.read"
] as const;

export function SecurityAssignmentsPage() {
  const adminPermissions = getAdminPermissionState();
  const {
    assignments,
    setAssignments,
    isLoading,
    readError,
    clearReadError,
    loadAssignments
  } = useSecurityAssignmentsData(adminPermissions.canManageSecurityAssignments);
  const [isSaving, setIsSaving] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [editingHash, setEditingHash] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<SecurityAssignmentFieldErrors>({});
  const [confirmAction, setConfirmAction] = useState<ConfirmAction | null>(null);
  const [form, setForm] = useState({
    name: "",
    credentialKey: "",
    roles: [] as string[],
    permissions: [] as string[],
    isEnabled: true
  });

  const resetForm = () => {
    setEditingHash(null);
    setForm({
      name: "",
      credentialKey: "",
      roles: [],
      permissions: [],
      isEnabled: true
    });
    setFieldErrors({});
  };

  const refreshAssignments = async () => {
    setErrorMessage(null);
    await loadAssignments();
  };

  const startEdit = (assignment: SecurityAssignment) => {
    setEditingHash(assignment.credentialKeyHash);
    setForm({
      name: assignment.name,
      credentialKey: "",
      roles: assignment.roles,
      permissions: assignment.permissions,
      isEnabled: assignment.isEnabled
    });
    setFieldErrors({});
  };

  const toggleFormValue = (field: "roles" | "permissions", value: string) => {
    setForm((current) => {
      const values = new Set(current[field]);
      if (values.has(value)) {
        values.delete(value);
      } else {
        values.add(value);
      }

      return {
        ...current,
        [field]: Array.from(values)
      };
    });
  };

  const validateForm = () => {
    const nextErrors: SecurityAssignmentFieldErrors = {};
    if (!form.name.trim()) {
      nextErrors.name = "Name this assignment.";
    }

    if (!form.credentialKey.trim()) {
      nextErrors.credentialKey = "Enter the credential key to store its hash.";
    }

    return nextErrors;
  };

  const saveAssignment = async () => {
    const nextErrors = validateForm();
    if (nextErrors.name || nextErrors.credentialKey) {
      setFieldErrors(nextErrors);
      return;
    }

    setIsSaving(true);
    setErrorMessage(null);
    clearReadError();
    setFieldErrors({});

    try {
      const assignment = await upsertSecurityAssignment({
        name: form.name.trim(),
        credentialKey: form.credentialKey.trim(),
        roles: form.roles,
        permissions: form.permissions,
        isEnabled: form.isEnabled
      });

      setAssignments((current) => {
        const withoutCurrent = current.filter(
          (item) => item.credentialKeyHash !== assignment.credentialKeyHash
        );
        return [...withoutCurrent, assignment].sort((left, right) =>
          left.name.localeCompare(right.name)
        );
      });
      resetForm();
      showToast({
        title: "Security assignment saved",
        message: assignment.name,
        variant: "success"
      });
    } catch (error) {
      if (error instanceof ApiError) {
        setErrorMessage(toFriendlyErrorMessage(error.errorCode, error.message));
      } else {
        setErrorMessage("Security assignment could not be saved.");
      }
    } finally {
      setIsSaving(false);
    }
  };

  const disableAssignment = async (assignment: SecurityAssignment) => {
    setErrorMessage(null);
    clearReadError();

    try {
      const disabled = await disableSecurityAssignment(assignment.credentialKeyHash);
      setAssignments((current) =>
        current.map((item) =>
          item.credentialKeyHash === disabled.credentialKeyHash
            ? { ...item, isEnabled: disabled.isEnabled }
            : item
        )
      );
      showToast({
        title: "Security assignment disabled",
        message: assignment.name,
        variant: "success"
      });
      if (editingHash === assignment.credentialKeyHash) {
        resetForm();
      }
    } catch (error) {
      if (error instanceof ApiError) {
        setErrorMessage(toFriendlyErrorMessage(error.errorCode, error.message));
      } else {
        setErrorMessage("Security assignment could not be disabled.");
      }
    }
  };

  const feedbackMessage = errorMessage ?? readError;

  const requestDisable = (assignment: SecurityAssignment) => {
    setConfirmAction({
      title: "Disable security assignment?",
      description: `Disable ${assignment.name}? Requests using this credential will be rejected.`,
      confirmLabel: "Disable",
      variant: "destructive",
      onConfirm: () => void disableAssignment(assignment)
    });
  };

  if (!adminPermissions.canManageSecurityAssignments) {
    return (
      <Card className="admin-panel">
        <CardHeader className="panel-heading-wide">
          <div className="card-title-row">
            <span className="card-glyph" aria-hidden="true" />
            <div>
              <p className="eyebrow">Security</p>
              <CardTitle>Manage assignments.</CardTitle>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          <EmptyState
            title="Security management is unavailable"
            description="Only the Admin role can manage security assignments."
          />
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className="admin-panel">
      <CardHeader className="panel-heading-wide">
        <div className="card-title-row">
          <span className="card-glyph" aria-hidden="true" />
          <div>
            <p className="eyebrow">Security</p>
            <CardTitle>Manage user, role, and permission assignments.</CardTitle>
          </div>
        </div>
        <RefreshButton isRefreshing={isLoading} label="Refresh security assignments" onRefresh={refreshAssignments} />
      </CardHeader>

      <CardContent>
        {feedbackMessage ? (
          <p className="feedback feedback-error">{feedbackMessage}</p>
        ) : null}

        <div className="security-dialog-grid">
          <div className="security-assignment-form">
            <div>
              <p className="eyebrow">{editingHash ? "Update" : "Create"}</p>
              <h3>{editingHash ? "Update assignment" : "New assignment"}</h3>
              {editingHash ? (
                <p className="muted-copy">Re-enter the credential key to update the stored hash.</p>
              ) : null}
            </div>

            <FormField id="assignment-name" label="Assignment name" required value={form.name} error={fieldErrors.name} onChange={(value) => {
                  setForm((current) => ({
                    ...current,
                    name: value
                  }));
                  setFieldErrors((current) => ({
                    ...current,
                    name: undefined
                  }));
                }} />

            <FormField id="assignment-credential-key" label="Credential key" required type="password" value={form.credentialKey} error={fieldErrors.credentialKey} placeholder={editingHash ? "Enter key again to update" : "New admin API key"} onChange={(value) => {
                  setForm((current) => ({
                    ...current,
                    credentialKey: value
                  }));
                  setFieldErrors((current) => ({
                    ...current,
                    credentialKey: undefined
                  }));
                }} />

            <fieldset className="security-choice-group">
              <legend>System roles</legend>
              {systemRoleOptions.map((role) => (
                <label key={role} className="security-choice">
                  <input
                    type="checkbox"
                    checked={form.roles.includes(role)}
                    onChange={() => toggleFormValue("roles", role)}
                  />
                  <span>{role}</span>
                </label>
              ))}
            </fieldset>

            <fieldset className="security-choice-group security-permission-grid">
              <legend>Explicit permissions</legend>
              {permissionOptions.map((permission) => (
                <label key={permission} className="security-choice">
                  <input
                    type="checkbox"
                    checked={form.permissions.includes(permission)}
                    onChange={() => toggleFormValue("permissions", permission)}
                  />
                  <span>{permission}</span>
                </label>
              ))}
            </fieldset>

            <label className="security-choice security-enabled-choice">
              <input
                type="checkbox"
                checked={form.isEnabled}
                onChange={(event) =>
                  setForm((current) => ({
                    ...current,
                    isEnabled: event.target.checked
                  }))
                }
              />
              <span>Enabled</span>
            </label>

            <div className="dialog-actions">
              <Button variant="secondary" onClick={resetForm}>
                Clear
              </Button>
              <Button disabled={isSaving} onClick={() => void saveAssignment()}>
                {isSaving ? "Saving" : "Save assignment"}
              </Button>
            </div>
          </div>

          <div className="security-assignment-list">
            <div className="security-list-header">
              <div>
                <p className="eyebrow">Assignments</p>
                <h3>Persisted credentials</h3>
              </div>
            </div>

            {isLoading ? (
              <div className="analytics-loading">
                <span className="skeleton skeleton-url" />
                <span className="skeleton skeleton-url" />
                <span className="skeleton skeleton-url" />
              </div>
            ) : null}

            {!isLoading && assignments.length === 0 ? (
              <EmptyState
                title="No persisted assignments"
                description="Create an assignment to manage a credential outside bootstrap configuration."
              />
            ) : null}

            {!isLoading && assignments.length > 0 ? (
              <div className="security-assignment-items">
                {assignments.map((assignment) => (
                  <div className="security-assignment-item" key={assignment.credentialKeyHash}>
                    <div className="security-assignment-item-header">
                      <div>
                        <strong>{assignment.name}</strong>
                        <code>{assignment.credentialKeyHash}</code>
                      </div>
                      <Badge variant={assignment.isEnabled ? "default" : "destructive"}>
                        {assignment.isEnabled ? "Enabled" : "Disabled"}
                      </Badge>
                    </div>
                    <dl>
                      <div>
                        <dt>Roles</dt>
                        <dd>{assignment.roles.length > 0 ? assignment.roles.join(", ") : "None"}</dd>
                      </div>
                      <div>
                        <dt>Permissions</dt>
                        <dd>
                          {assignment.permissions.length > 0
                            ? assignment.permissions.join(", ")
                            : "None"}
                        </dd>
                      </div>
                      <div>
                        <dt>Created</dt>
                        <dd>{formatDateTime(assignment.createdAtUtc)}</dd>
                      </div>
                    </dl>
                    <div className="security-assignment-actions">
                      <Button variant="secondary" onClick={() => startEdit(assignment)}>
                        Edit
                      </Button>
                      <Button
                        variant="destructive"
                        disabled={!assignment.isEnabled}
                        onClick={() => requestDisable(assignment)}
                      >
                        Disable
                      </Button>
                    </div>
                  </div>
                ))}
              </div>
            ) : null}
          </div>
        </div>
      </CardContent>

      <ConfirmDialog
        open={confirmAction !== null}
        title={confirmAction?.title ?? ""}
        description={confirmAction?.description ?? ""}
        confirmLabel={confirmAction?.confirmLabel ?? "Confirm"}
        variant={confirmAction?.variant}
        onConfirm={() => {
          const action = confirmAction;
          setConfirmAction(null);
          action?.onConfirm();
        }}
        onCancel={() => setConfirmAction(null)}
      />
    </Card>
  );
}
