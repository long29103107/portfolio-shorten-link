import { useCallback, useEffect, useRef, useState } from "react";
import { listAuditLogActions, listAuditLogEvents } from "../api/shortLinksApi";
import {
  emptyAuditLogFilters,
  mergeAuditLogEvents
} from "../domain/auditDiscovery";
import type {
  AuditLogEvent,
  AuditLogFilters
} from "../types";
import { createRecoveryNotice, type RecoveryNotice } from "@/shared/api/recovery";

const AUDIT_LOG_PAGE_LIMIT = 50;

export function useAuditLogData(filters: AuditLogFilters) {
  const [actions, setActions] = useState<string[]>([]);
  const [events, setEvents] = useState<AuditLogEvent[]>([]);
  const [nextCursor, setNextCursor] = useState<string | null>(null);
  const [failure, setFailure] = useState<RecoveryNotice | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isLoadingOlder, setIsLoadingOlder] = useState(false);
  const [reloadVersion, setReloadVersion] = useState(0);
  const requestVersion = useRef(0);
  const actionsController = useRef<AbortController | null>(null);
  const olderController = useRef<AbortController | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    actionsController.current?.abort();
    actionsController.current = controller;

    void listAuditLogActions(controller.signal)
      .then((response) => {
        if (!controller.signal.aborted) {
          setActions(response.items);
        }
      })
      .catch(() => {
        if (!controller.signal.aborted) {
          setActions([]);
        }
      });

    return () => controller.abort();
  }, []);

  useEffect(() => {
    const version = ++requestVersion.current;
    const controller = new AbortController();
    olderController.current?.abort();
    setIsLoading(true);
    setIsLoadingOlder(false);
    setFailure(null);
    setEvents([]);
    setNextCursor(null);

    void listAuditLogEvents({
      limit: AUDIT_LOG_PAGE_LIMIT,
      filters: { ...emptyAuditLogFilters, ...filters }
    }, controller.signal)
      .then((page) => {
        if (controller.signal.aborted || version !== requestVersion.current) return;
        setEvents(page.items);
        setNextCursor(page.nextCursor);
      })
      .catch((error) => {
        if (controller.signal.aborted || version !== requestVersion.current) return;
        setFailure(createRecoveryNotice(
          error,
          error instanceof Error ? error.message : "Audit events could not be loaded."
        ));
      })
      .finally(() => {
        if (!controller.signal.aborted && version === requestVersion.current) {
          setIsLoading(false);
        }
      });

    return () => controller.abort();
  }, [filters, reloadVersion]);

  const loadOlder = useCallback(async () => {
    if (!nextCursor || isLoadingOlder) return;

    const version = requestVersion.current;
    const controller = new AbortController();
    olderController.current?.abort();
    olderController.current = controller;
    setIsLoadingOlder(true);
    setFailure(null);

    try {
      const page = await listAuditLogEvents({
        limit: AUDIT_LOG_PAGE_LIMIT,
        cursor: nextCursor,
        filters: { ...emptyAuditLogFilters, ...filters }
      }, controller.signal);
      if (controller.signal.aborted || version !== requestVersion.current) return;
      setEvents((current) => mergeAuditLogEvents(current, page.items));
      setNextCursor(page.nextCursor);
    } catch (error) {
      if (controller.signal.aborted || version !== requestVersion.current) return;
      setFailure(createRecoveryNotice(
        error,
        error instanceof Error ? error.message : "Older audit events could not be loaded."
      ));
    } finally {
      if (!controller.signal.aborted && version === requestVersion.current) {
        setIsLoadingOlder(false);
      }
    }
  }, [filters, isLoadingOlder, nextCursor]);

  const retry = useCallback(() => {
    if (events.length > 0 && nextCursor) {
      void loadOlder();
      return;
    }

    setReloadVersion((version) => version + 1);
  }, [events.length, loadOlder, nextCursor]);

  return {
    actions,
    events,
    nextCursor,
    failure,
    isLoading,
    isLoadingOlder,
    loadOlder,
    retry
  };
}
