import { useCallback, useEffect, useRef, useState } from "react";
import { ApiError } from "../api/http";
import { listSecurityAssignments } from "../api/shortLinksApi";
import type { SecurityAssignment } from "../types";
import { toFriendlyErrorMessage } from "../types";

export function useSecurityAssignmentsData(canManageSecurityAssignments: boolean) {
  const [isLoading, setIsLoading] = useState(false);
  const [readError, setReadError] = useState<string | null>(null);
  const [assignments, setAssignments] = useState<SecurityAssignment[]>([]);
  const requestVersion = useRef(0);
  const activeController = useRef<AbortController | null>(null);
  const clearReadError = useCallback(() => setReadError(null), []);

  const loadAssignments = useCallback(async () => {
    activeController.current?.abort();
    const controller = new AbortController();
    activeController.current = controller;
    const version = ++requestVersion.current;
    setIsLoading(true);
    setReadError(null);

    try {
      if (!canManageSecurityAssignments) return;

      const result = await listSecurityAssignments(controller.signal);
      if (controller.signal.aborted || version !== requestVersion.current) return;
      setAssignments(result.items);
    } catch (error) {
      if (controller.signal.aborted || version !== requestVersion.current) return;
      if (error instanceof ApiError) {
        setReadError(toFriendlyErrorMessage(error.errorCode, error.message));
      } else {
        setReadError("Security assignments could not be loaded.");
      }
    } finally {
      if (!controller.signal.aborted && version === requestVersion.current) {
        setIsLoading(false);
      }
    }
  }, [canManageSecurityAssignments]);

  useEffect(() => {
    void loadAssignments();
    return () => {
      requestVersion.current += 1;
      activeController.current?.abort();
    };
  }, [loadAssignments]);

  return {
    assignments,
    setAssignments,
    isLoading,
    readError,
    clearReadError,
    loadAssignments
  };
}
