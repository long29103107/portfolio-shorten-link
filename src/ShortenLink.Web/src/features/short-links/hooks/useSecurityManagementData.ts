import { useCallback, useEffect, useRef, useState } from "react";
import { listSecurityRoles, listSecurityUsers } from "../api/shortLinksApi";
import type { SecurityRole, SecurityUser } from "../types";
import { createRecoveryNotice, type RecoveryNotice } from "@/shared/api/recovery";
import { ApiError } from "../api/http";
import { toFriendlyErrorMessage } from "../types";

export function useSecurityManagementData(canManageSecurityAssignments: boolean) {
  const [isLoading, setIsLoading] = useState(false);
  const [readFailure, setReadFailure] = useState<RecoveryNotice | null>(null);
  const [users, setUsers] = useState<SecurityUser[]>([]);
  const [systemRoles, setSystemRoles] = useState<SecurityRole[]>([]);
  const [customRoles, setCustomRoles] = useState<SecurityRole[]>([]);
  const requestVersion = useRef(0);
  const activeController = useRef<AbortController | null>(null);

  const loadSecurity = useCallback(async () => {
    activeController.current?.abort();
    const controller = new AbortController();
    activeController.current = controller;
    const version = ++requestVersion.current;
    setIsLoading(true);
    setReadFailure(null);

    try {
      if (!canManageSecurityAssignments) return;
      const [rolesResult, usersResult] = await Promise.all([
        listSecurityRoles(controller.signal),
        listSecurityUsers(controller.signal)
      ]);
      if (controller.signal.aborted || version !== requestVersion.current) return;
      setSystemRoles(rolesResult.systemRoles);
      setCustomRoles(rolesResult.customRoles);
      setUsers(usersResult.items);
    } catch (error) {
      if (controller.signal.aborted || version !== requestVersion.current) return;
      const message = error instanceof ApiError
        ? toFriendlyErrorMessage(error.errorCode, error.message)
        : "Security data could not be loaded.";
      setReadFailure(createRecoveryNotice(error, message));
    } finally {
      if (!controller.signal.aborted && version === requestVersion.current) {
        setIsLoading(false);
      }
    }
  }, [canManageSecurityAssignments]);

  useEffect(() => {
    void loadSecurity();
    return () => {
      requestVersion.current += 1;
      activeController.current?.abort();
    };
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
