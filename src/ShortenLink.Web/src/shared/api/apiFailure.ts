import { HTTP_STATUS } from "../constants/http";

export type ApiFailureKind =
  | "network"
  | "timeout"
  | "rate-limit"
  | "server"
  | "validation"
  | "authentication"
  | "authorization"
  | "not-found"
  | "unexpected";

export type ApiFailure = {
  kind: ApiFailureKind;
  status: number | null;
  errorCode: string;
  message: string;
  retryable: boolean;
  shouldNavigateToAuth: boolean;
  fieldErrors: Record<string, string>;
};

export type ApiFailurePayload = {
  errorCode: string;
  message: string;
  fieldErrors?: Record<string, string | string[]>;
};

export function classifyHttpFailure(status: number, payload: ApiFailurePayload): ApiFailure {
  if (status === HTTP_STATUS.UNAUTHORIZED) {
    return createFailure("authentication", status, payload, false, true);
  }

  if (status === HTTP_STATUS.FORBIDDEN) {
    return createFailure("authorization", status, payload, false, true);
  }

  if (status === HTTP_STATUS.NOT_FOUND) {
    return createFailure("not-found", status, payload, false, false);
  }

  if (status === HTTP_STATUS.REQUEST_TIMEOUT) {
    return createFailure("timeout", status, payload, true, false);
  }

  if (status === HTTP_STATUS.TOO_MANY_REQUESTS) {
    return createFailure("rate-limit", status, payload, true, false);
  }

  if (status >= 500) {
    return createFailure("server", status, payload, true, false);
  }

  if (
    status === HTTP_STATUS.BAD_REQUEST
    || status === HTTP_STATUS.CONFLICT
    || status === HTTP_STATUS.UNPROCESSABLE_ENTITY
  ) {
    return createFailure("validation", status, payload, false, false);
  }

  return createFailure("unexpected", status, payload, false, false);
}

export function classifyFetchFailure(error: unknown): ApiFailure {
  if (isAbortError(error)) {
    return {
      kind: "timeout",
      status: null,
      errorCode: "request_timeout",
      message: "The request timed out.",
      retryable: true,
      shouldNavigateToAuth: false,
      fieldErrors: {}
    };
  }

  return {
    kind: "network",
    status: null,
    errorCode: "network_error",
    message: "The server could not be reached.",
    retryable: true,
    shouldNavigateToAuth: false,
    fieldErrors: {}
  };
}

function createFailure(
  kind: ApiFailureKind,
  status: number,
  payload: ApiFailurePayload,
  retryable: boolean,
  shouldNavigateToAuth: boolean
): ApiFailure {
  return {
    kind,
    status,
    errorCode: payload.errorCode,
    message: payload.message,
    retryable,
    shouldNavigateToAuth,
    fieldErrors: normalizeFieldErrors(payload.fieldErrors)
  };
}

function normalizeFieldErrors(
  fieldErrors: ApiFailurePayload["fieldErrors"]
): Record<string, string> {
  if (!fieldErrors) {
    return {};
  }

  return Object.fromEntries(
    Object.entries(fieldErrors).flatMap(([field, messages]) => {
      const message = Array.isArray(messages) ? messages.find(Boolean) : messages;
      return message ? [[field, message]] : [];
    })
  );
}

function isAbortError(error: unknown) {
  return typeof error === "object"
    && error !== null
    && "name" in error
    && error.name === "AbortError";
}
