import type { ApiErrorPayload, SecurityLoginResponse } from "../types";
import { showToast } from "../../../shared/toast";
import {
  classifyFetchFailure,
  classifyHttpFailure,
  type ApiFailure
} from "../../../shared/api/apiFailure";
import { clearStoredSession, getAdminApiKeyHeader, getStoredRefreshToken, storeSession } from "./adminSecurity";
import type { ApiRequestOptions } from "../../../shared/api/apiClient";
import { HTTP_HEADERS, HTTP_METHODS, HTTP_STATUS } from "../../../shared/constants/http";
import { APP_ROUTES } from "../../../shared/constants/routes";
import { SHORT_LINK_API_ROUTES } from "../constants/apiRoutes";

export type FetchJsonOptions = ApiRequestOptions;

let refreshPromise: Promise<boolean> | null = null;

export class ApiError extends Error {
  readonly status: number | null;
  readonly errorCode: string;
  readonly kind: ApiFailure["kind"];
  readonly retryable: boolean;
  readonly shouldNavigateToAuth: boolean;
  readonly failure: ApiFailure;
  readonly fieldErrors: Record<string, string>;

  constructor(failure: ApiFailure) {
    super(failure.message);
    this.name = "ApiError";
    this.status = failure.status;
    this.errorCode = failure.errorCode;
    this.kind = failure.kind;
    this.retryable = failure.retryable;
    this.shouldNavigateToAuth = failure.shouldNavigateToAuth;
    this.failure = failure;
    this.fieldErrors = failure.fieldErrors;
  }
}

export async function fetchJson<T>(input: RequestInfo | URL, init?: FetchJsonOptions): Promise<T> {
  const { suppressAuthRedirect = false, skipRefresh = false, ...requestInit } = init ?? {};
  let response: Response;
  try {
    response = await fetch(input, {
      ...requestInit,
      headers: {
        [HTTP_HEADERS.CONTENT_TYPE]: HTTP_HEADERS.JSON,
        ...getAdminApiKeyHeader(),
        ...(requestInit.headers ?? {})
      }
    });
  } catch (error) {
    const failure = classifyFetchFailure(error);
    showFailureToast(failure);
    throw new ApiError(failure);
  }

  if (response.ok) {
    if (response.status === HTTP_STATUS.NO_CONTENT) {
      return undefined as T;
    }

    return (await response.json()) as T;
  }

  if (response.status === HTTP_STATUS.UNAUTHORIZED && !suppressAuthRedirect && !skipRefresh) {
    const refreshed = await refreshSession();
    if (refreshed) {
      return fetchJson<T>(input, { ...init, skipRefresh: true });
    }
  }

  const payload = (await safeReadError(response)) ?? {
    errorCode: "unexpected_error",
    message: `Request failed with status ${response.status}.`
  };
  const failure = classifyHttpFailure(response.status, payload);

  if (failure.shouldNavigateToAuth && !suppressAuthRedirect) {
    if (response.status === HTTP_STATUS.UNAUTHORIZED) {
      clearStoredSession();
    }
    navigateToStatusPage(response.status);
    throw new ApiError(failure);
  }

  showFailureToast(failure);

  throw new ApiError(failure);
}

function showFailureToast(failure: ApiFailure) {
  showToast({
    title: failure.retryable ? "Server temporarily unavailable" : "Request failed",
    message: failure.message,
    variant: "error"
  });
}

async function safeReadError(response: Response): Promise<ApiErrorPayload | null> {
  try {
    return (await response.json()) as ApiErrorPayload;
  } catch {
    return null;
  }
}

function navigateToStatusPage(status: number) {
  const path = status === HTTP_STATUS.UNAUTHORIZED ? APP_ROUTES.LOGIN : APP_ROUTES.FORBIDDEN;
  if (window.location.pathname !== path) {
    window.history.pushState({}, "", path);
  }

  window.dispatchEvent(new PopStateEvent("popstate"));
}

async function refreshSession(): Promise<boolean> {
  if (refreshPromise) {
    return refreshPromise;
  }

  refreshPromise = performRefresh();
  try {
    return await refreshPromise;
  } finally {
    refreshPromise = null;
  }
}

async function performRefresh(): Promise<boolean> {
  const refreshToken = getStoredRefreshToken();
  if (!refreshToken) {
    return false;
  }

  try {
    const response = await fetch(SHORT_LINK_API_ROUTES.REFRESH, {
      method: HTTP_METHODS.POST,
      headers: { [HTTP_HEADERS.CONTENT_TYPE]: HTTP_HEADERS.JSON },
      body: JSON.stringify({ refreshToken })
    });
    if (!response.ok) {
      clearStoredSession();
      return false;
    }

    const session = await response.json() as SecurityLoginResponse;
    storeSession(session.accessToken, session.refreshToken, session.user);
    return true;
  } catch {
    return false;
  }
}
