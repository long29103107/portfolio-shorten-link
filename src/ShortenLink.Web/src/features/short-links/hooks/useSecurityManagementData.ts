import { useCallback, useEffect, useState } from "react";
import { listSecurityRoles, listSecurityUsers } from "../api/shortLinksApi";
import type { SecurityRole, SecurityUser } from "../types";
import { createRecoveryNotice, type RecoveryNotice } from "../../../shared/api/recovery";
import { ApiError } from "../api/http";
import { toFriendlyErrorMessage } from "../types";

export function useSecurityManagementData(canManageSecurityAssignments: boolean) {
  const [isLoading, setIsLoading] = useState(false);
  const [readFailure, setReadFailure] = useState<RecoveryNotice | null>(null);
  const [users, setUsers] = useState<SecurityUser[]>([]);
  const [systemRoles, setSystemRoles] = useState<SecurityRole[]>([]);
  const [customRoles, setCustomRoles] = useState<SecurityRole[]>([]);

  const loadSecurity = useCallback(async () => {
    setIsLoading(true);
    setReadFailure(null);
    try {
      if (!canManageSecurityAssignments) return;
      const [rolesResult, usersResult] = await Promise.all([listSecurityRoles(), listSecurityUsers()]);
      setSystemRoles(rolesResult.systemRoles);
      setCustomRoles(rolesResult.customRoles);
      setUsers(usersResult.items);
    } catch (error) {
      const message = error instanceof ApiError
        ? toFriendlyErrorMessage(error.errorCode, error.message)
        : "Security data could not be loaded.";
      setReadFailure(createRecoveryNotice(error, message));
    } finally {
      setIsLoading(false);
    }
  }, [canManageSecurityAssignments]);

  useEffect(() => {
    void loadSecurity();
  }, [loadSecurity]);

  return {
    isLoading,
    readFailure,
    users,
    setUsers,
    systemRoles,
    setSystemRoles,
    customRoles,
    setCustomRoles,
    loadSecurity
  };
}
