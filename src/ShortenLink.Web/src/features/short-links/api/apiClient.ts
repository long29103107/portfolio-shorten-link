import { useMemo } from "react";
import {
  buildApiUrl,
  createApiClient,
  type ApiClient,
  type ApiClientRequestOptions,
  type ApiQuery,
  type ApiQueryValue
} from "../../../shared/api/apiClient";
import { fetchJson } from "./http";

export type { ApiClient, ApiClientRequestOptions, ApiQuery, ApiQueryValue };
export { buildApiUrl };

export const apiClient = createApiClient(fetchJson);

/** React access point for components that need the feature API client. */
export function useApi() {
  return useMemo(() => apiClient, []);
}
