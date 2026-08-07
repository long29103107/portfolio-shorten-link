import { useCallback, useEffect, useState } from "react";
import { ApiError } from "../api/http";
import { listShortLinks } from "../api/shortLinksApi";
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

  const loadLinks = useCallback(async (nextPageNumber = 1, signal?: AbortSignal) => {
    setIsLoading(true);
    setListFailure(null);

    try {
      const result = await listShortLinks(pageSize, nextPageNumber, discoveryQuery, signal);
      setLinks(result.items);
      setTotalCount(result.totalCount ?? result.items.length);
      setTotalPages(result.totalPages ?? 1);
      setPageNumber(result.page ?? nextPageNumber);
      return result;
    } catch (error) {
      if (signal?.aborted) return null;
      const message = error instanceof ApiError
        ? toFriendlyErrorMessage(error.errorCode, error.message)
        : "We could not load links right now.";
      setListFailure({
        ...createRecoveryNotice(error, message),
        pageNumber: nextPageNumber
      });
      return null;
    } finally {
      if (!signal?.aborted) setIsLoading(false);
    }
  }, [discoveryQuery, pageSize]);

  useEffect(() => {
    const controller = new AbortController();
    void loadLinks(1, controller.signal);
    return () => controller.abort();
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
