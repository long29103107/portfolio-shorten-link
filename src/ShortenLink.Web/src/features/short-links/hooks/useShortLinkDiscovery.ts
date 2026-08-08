import { useCallback, useEffect, useRef, useState } from "react";
import { ApiError } from "../api/http";
import { listShortLinks } from "../api/shortLinksApi";
import { isCurrentRequestGeneration } from "../domain/requestLifecycle";
import type { ShortLinkAdminItem, ShortLinkDiscoveryQuery } from "../types";
import { toFriendlyErrorMessage } from "../types";
import { createRecoveryNotice, type RecoveryNotice } from "../../../shared/api/recovery";
import {
  defaultShortLinkDiscoveryQuery
} from "../components/ShortLinkDiscoveryToolbar";

export type ShortLinkDiscoveryFailure = RecoveryNotice & { pageNumber: number };

export function useShortLinkDiscovery() {
  const [links, setLinks] = useState<ShortLinkAdminItem[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [listFailure, setListFailure] = useState<ShortLinkDiscoveryFailure | null>(null);
  const [pageSize, setPageSize] = useState(25);
  const [pageNumber, setPageNumber] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(1);
  const [discoveryQuery, setDiscoveryQuery] = useState<ShortLinkDiscoveryQuery>(
    defaultShortLinkDiscoveryQuery
  );
  const requestVersion = useRef(0);
  const activeController = useRef<AbortController | null>(null);

  const loadLinks = useCallback(async (nextPageNumber = 1) => {
    activeController.current?.abort();
    const controller = new AbortController();
    activeController.current = controller;
    const version = ++requestVersion.current;
    setIsLoading(true);
    setListFailure(null);

    try {
      const result = await listShortLinks(pageSize, nextPageNumber, discoveryQuery, controller.signal);
      if (!isCurrentRequestGeneration(version, requestVersion.current, controller.signal)) {
        return null;
      }
      setLinks(result.items);
      setTotalCount(result.totalCount ?? result.items.length);
      setTotalPages(result.totalPages ?? 1);
      setPageNumber(result.page ?? nextPageNumber);
      return result;
    } catch (error) {
      if (!isCurrentRequestGeneration(version, requestVersion.current, controller.signal)) return null;
      const message = error instanceof ApiError
        ? toFriendlyErrorMessage(error.errorCode, error.message)
        : "We could not load links right now.";
      setListFailure({
        ...createRecoveryNotice(error, message),
        pageNumber: nextPageNumber
      });
      return null;
    } finally {
      if (isCurrentRequestGeneration(version, requestVersion.current, controller.signal)) {
        setIsLoading(false);
        if (activeController.current === controller) {
          activeController.current = null;
        }
      }
    }
  }, [discoveryQuery, pageSize]);

  useEffect(() => {
    void loadLinks(1);
    return () => {
      requestVersion.current += 1;
      activeController.current?.abort();
      activeController.current = null;
    };
  }, [loadLinks]);

  return {
    links,
    setLinks,
    isLoading,
    listFailure,
    loadLinks,
    pageSize,
    setPageSize,
    pageNumber,
    setPageNumber,
    totalCount,
    totalPages,
    discoveryQuery,
    setDiscoveryQuery
  };
}
