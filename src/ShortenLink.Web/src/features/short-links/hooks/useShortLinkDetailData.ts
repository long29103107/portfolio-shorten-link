import { useEffect, useRef, useState } from "react";
import { ApiError } from "../api/http";
import { getShortLinkDetails } from "../api/shortLinksApi";
import type { ShortLinkDetails } from "../types";
import { toFriendlyErrorMessage } from "../types";

export function useShortLinkDetailData(code: string) {
  const [details, setDetails] = useState<ShortLinkDetails | null>(null);
  const [readError, setReadError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const requestVersion = useRef(0);

  useEffect(() => {
    const controller = new AbortController();
    const version = ++requestVersion.current;
    setDetails(null);
    setIsLoading(true);
    setReadError(null);

    void getShortLinkDetails(code, controller.signal)
      .then((response) => {
        if (controller.signal.aborted || version !== requestVersion.current) return;
        setDetails(response);
      })
      .catch((error) => {
        if (controller.signal.aborted || version !== requestVersion.current) return;
        if (error instanceof ApiError) {
          setReadError(toFriendlyErrorMessage(error.errorCode, error.message));
        } else {
          setReadError("We could not load this short link right now.");
        }
      })
      .finally(() => {
        if (!controller.signal.aborted && version === requestVersion.current) {
          setIsLoading(false);
        }
      });

    return () => {
      requestVersion.current += 1;
      controller.abort();
    };
  }, [code]);

  return {
    details,
    setDetails,
    readError,
    isLoading
  };
}
