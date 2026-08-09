import { useCallback, useEffect, useRef, useState } from "react";
import { ApiError } from "../api/http";
import { listShortLinks } from "../api/shortLinksApi";
import { downloadShortLinksCsv } from "../domain/export";
import type { ShortLinkAdminItem, ShortLinkDiscoveryQuery } from "../types";
import { toFriendlyErrorMessage } from "../types";
import { createRecoveryNotice, type RecoveryNotice } from "@/shared/api/recovery";
import { showToast } from "@/shared/toast";

export function useShortLinkExport(discoveryQuery: ShortLinkDiscoveryQuery) {
  const [isExporting, setIsExporting] = useState(false);
  const [exportFailure, setExportFailure] = useState<RecoveryNotice | null>(null);
  const requestVersion = useRef(0);
  const activeController = useRef<AbortController | null>(null);

  const cancelExport = useCallback(() => {
    requestVersion.current += 1;
    activeController.current?.abort();
    setIsExporting(false);
  }, []);

  const handleExport = useCallback(async () => {
    if (isExporting) {
      return;
    }

    activeController.current?.abort();
    const controller = new AbortController();
    activeController.current = controller;
    const version = ++requestVersion.current;
    setIsExporting(true);
    setExportFailure(null);

    try {
      const firstPage = await listShortLinks(200, 1, discoveryQuery, controller.signal);
      const allLinks: ShortLinkAdminItem[] = [...firstPage.items];
      const totalPages = Math.max(firstPage.totalPages ?? 1, 1);

      for (let page = 2; page <= totalPages; page += 1) {
        if (controller.signal.aborted || version !== requestVersion.current) return;
        const nextPage = await listShortLinks(200, page, discoveryQuery, controller.signal);
        const knownCodes = new Set(allLinks.map((link) => link.code));
        allLinks.push(...nextPage.items.filter((link) => !knownCodes.has(link.code)));
      }

      if (controller.signal.aborted || version !== requestVersion.current) return;
      downloadShortLinksCsv(allLinks);
      showToast({
        title: "Short links exported",
        message: `${allLinks.length} link${allLinks.length === 1 ? "" : "s"} downloaded`,
        variant: "success"
      });
    } catch (error) {
      if (controller.signal.aborted || version !== requestVersion.current) return;
      const message = error instanceof ApiError
        ? toFriendlyErrorMessage(error.errorCode, error.message)
        : "The short-link export could not be created.";
      setExportFailure(createRecoveryNotice(error, message));
    } finally {
      if (!controller.signal.aborted && version === requestVersion.current) {
        setIsExporting(false);
      }
    }
  }, [discoveryQuery, isExporting]);

  const clearExportFailure = useCallback(() => setExportFailure(null), []);

  useEffect(() => () => {
    requestVersion.current += 1;
    activeController.current?.abort();
  }, []);

  return {
    isExporting,
    exportFailure,
    handleExport,
    cancelExport,
    clearExportFailure
  };
}
