import { useCallback, useEffect, useRef, useState } from "react";
import { listAuditLogActions, listAuditLogEvents } from "../api/shortLinksApi";
import { emptyAuditLogFilters } from "../domain/auditDiscovery";
import type { AuditLogEvent, AuditLogFilters, AuditLogPage } from "../types";
import { createRecoveryNotice, type RecoveryNotice } from "@/shared/api/recovery";

const AUDIT_LOG_PAGE_LIMIT = 50;

export function useAuditLogData(filters: AuditLogFilters) {
  const [actions, setActions] = useState<string[]>([]);
  const [events, setEvents] = useState<AuditLogEvent[]>([]);
  const [nextCursor, setNextCursor] = useState<string | null>(null);
  const [currentCursor, setCurrentCursor] = useState<string | null>(null);
  const [previousCursors, setPreviousCursors] = useState<Array<string | null>>([]);
  const [pageNumber, setPageNumber] = useState(1);
  const [failure, setFailure] = useState<RecoveryNotice | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isLoadingOlder, setIsLoadingOlder] = useState(false);
  const [reloadVersion, setReloadVersion] = useState(0);
  const requestVersion = useRef(0);
  const actionsController = useRef<AbortController | null>(null);
  const pageController = useRef<AbortController | null>(null);

  const fetchPage = useCallback((cursor: string | null, signal: AbortSignal): Promise<AuditLogPage> => (
    listAuditLogEvents({
      limit: AUDIT_LOG_PAGE_LIMIT,
      cursor,
      filters: { ...emptyAuditLogFilters, ...filters }
    }, signal)
  ), [filters]);

  useEffect(() => {
    const controller = new AbortController();
    actionsController.current?.abort();
    actionsController.current = controller;

    void listAuditLogActions(controller.signal)
      .then((response) => {
        if (!controller.signal.aborted) setActions(response.items);
      })
      .catch(() => {
        if (!controller.signal.aborted) setActions([]);
      });

    return () => controller.abort();
  }, []);

  useEffect(() => {
    const version = ++requestVersion.current;
    const controller = new AbortController();
    pageController.current?.abort();
    pageController.current = controller;
    setIsLoading(true);
    setIsLoadingOlder(false);
    setFailure(null);
    setEvents([]);
    setNextCursor(null);
    setCurrentCursor(null);
    setPreviousCursors([]);
    setPageNumber(1);

    void fetchPage(null, controller.signal)
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
        if (!controller.signal.aborted && version === requestVersion.current) setIsLoading(false);
      });

    return () => controller.abort();
  }, [fetchPage, reloadVersion]);

  const loadNext = useCallback(async () => {
    if (!nextCursor || isLoadingOlder) return;

    const cursor = nextCursor;
    const version = requestVersion.current;
    const controller = new AbortController();
    pageController.current?.abort();
    pageController.current = controller;
    setIsLoadingOlder(true);
    setFailure(null);

    try {
      const page = await fetchPage(cursor, controller.signal);
      if (controller.signal.aborted || version !== requestVersion.current) return;
      setEvents(page.items);
      setNextCursor(page.nextCursor);
      setPreviousCursors((current) => [...current, currentCursor]);
      setCurrentCursor(cursor);
      setPageNumber((current) => current + 1);
    } catch (error) {
      if (controller.signal.aborted || version !== requestVersion.current) return;
      setFailure(createRecoveryNotice(
        error,
        error instanceof Error ? error.message : "The next audit page could not be loaded."
      ));
    } finally {
      if (!controller.signal.aborted && version === requestVersion.current) setIsLoadingOlder(false);
    }
  }, [currentCursor, fetchPage, isLoadingOlder, nextCursor]);

  const loadPrevious = useCallback(async () => {
    if (previousCursors.length === 0 || isLoadingOlder) return;

    const cursor = previousCursors[previousCursors.length - 1] ?? null;
    const version = requestVersion.current;
    const controller = new AbortController();
    pageController.current?.abort();
    pageController.current = controller;
    setIsLoadingOlder(true);
    setFailure(null);

    try {
      const page = await fetchPage(cursor, controller.signal);
      if (controller.signal.aborted || version !== requestVersion.current) return;
      setEvents(page.items);
      setNextCursor(page.nextCursor);
      setPreviousCursors((current) => current.slice(0, -1));
      setCurrentCursor(cursor);
      setPageNumber((current) => Math.max(1, current - 1));
    } catch (error) {
      if (controller.signal.aborted || version !== requestVersion.current) return;
      setFailure(createRecoveryNotice(
        error,
        error instanceof Error ? error.message : "The previous audit page could not be loaded."
      ));
    } finally {
      if (!controller.signal.aborted && version === requestVersion.current) setIsLoadingOlder(false);
    }
  }, [fetchPage, isLoadingOlder, previousCursors]);

  const retry = useCallback(() => {
    if (events.length === 0) {
      setReloadVersion((version) => version + 1);
      return;
    }

    const version = requestVersion.current;
    const controller = new AbortController();
    pageController.current?.abort();
    pageController.current = controller;
    setIsLoadingOlder(true);
    setFailure(null);

    void fetchPage(currentCursor, controller.signal)
      .then((page) => {
        if (controller.signal.aborted || version !== requestVersion.current) return;
        setEvents(page.items);
        setNextCursor(page.nextCursor);
      })
      .catch((error) => {
        if (controller.signal.aborted || version !== requestVersion.current) return;
        setFailure(createRecoveryNotice(
          error,
          error instanceof Error ? error.message : "The audit page could not be loaded."
        ));
      })
      .finally(() => {
        if (!controller.signal.aborted && version === requestVersion.current) setIsLoadingOlder(false);
      });
  }, [currentCursor, events.length, fetchPage]);

  return {
    actions,
    events,
    nextCursor,
    hasPreviousPage: previousCursors.length > 0,
    pageNumber,
    failure,
    isLoading,
    isLoadingOlder,
    loadNext,
    loadPrevious,
    retry
  };
}
