import { useCallback, useEffect, useRef, useState } from "react";
import { getShortLinkAnalytics } from "../api/shortLinksApi";
import { ApiError } from "../api/http";
import type { ShortLinkAnalytics } from "../types";
import { toFriendlyErrorMessage } from "../types";

export function useShortLinkAnalyticsData() {
  const [analyticsCode, setAnalyticsCode] = useState<string | null>(null);
  const [analyticsData, setAnalyticsData] = useState<ShortLinkAnalytics | null>(null);
  const [analyticsError, setAnalyticsError] = useState<string | null>(null);
  const [isAnalyticsRetryable, setIsAnalyticsRetryable] = useState(false);
  const [isAnalyticsLoading, setIsAnalyticsLoading] = useState(false);
  const requestVersion = useRef(0);
  const activeController = useRef<AbortController | null>(null);

  const loadAnalytics = useCallback(async (code: string) => {
    activeController.current?.abort();
    const controller = new AbortController();
    activeController.current = controller;
    const version = ++requestVersion.current;
    setAnalyticsData(null);
    setAnalyticsError(null);
    setIsAnalyticsRetryable(false);
    setIsAnalyticsLoading(true);

    try {
      const analytics = await getShortLinkAnalytics(code, controller.signal);
      if (controller.signal.aborted || version !== requestVersion.current) return;
      setAnalyticsData(analytics);
    } catch (error) {
      if (controller.signal.aborted || version !== requestVersion.current) return;
      if (error instanceof ApiError) {
        setAnalyticsError(toFriendlyErrorMessage(error.errorCode, error.message));
        setIsAnalyticsRetryable(error.retryable);
      } else {
        setAnalyticsError("Analytics could not be loaded.");
      }
    } finally {
      if (!controller.signal.aborted && version === requestVersion.current) {
        setIsAnalyticsLoading(false);
      }
    }
  }, []);

  const openAnalytics = useCallback((code: string) => {
    setAnalyticsCode(code);
    void loadAnalytics(code);
  }, [loadAnalytics]);

  const closeAnalytics = useCallback(() => {
    requestVersion.current += 1;
    activeController.current?.abort();
    setAnalyticsCode(null);
    setAnalyticsData(null);
    setAnalyticsError(null);
    setIsAnalyticsRetryable(false);
    setIsAnalyticsLoading(false);
  }, []);

  const retryAnalytics = useCallback(() => {
    if (analyticsCode) {
      void loadAnalytics(analyticsCode);
    }
  }, [analyticsCode, loadAnalytics]);

  useEffect(() => () => {
    requestVersion.current += 1;
    activeController.current?.abort();
  }, []);

  return {
    analyticsCode,
    analyticsData,
    analyticsError,
    isAnalyticsRetryable,
    isAnalyticsLoading,
    openAnalytics,
    closeAnalytics,
    retryAnalytics
  };
}
