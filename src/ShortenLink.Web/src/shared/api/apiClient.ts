import { HTTP_METHODS, type HttpMethod } from "../constants/http";

export type ApiQueryValue = string | number | boolean | null | undefined;
export type ApiQuery = Record<string, ApiQueryValue | readonly ApiQueryValue[]>;
export type ApiRequestOptions = RequestInit & {
  suppressAuthRedirect?: boolean;
  skipRefresh?: boolean;
};
export type ApiClientRequestOptions = Omit<ApiRequestOptions, "body" | "method">;
export type ApiTransport = <T>(input: RequestInfo | URL, init?: ApiRequestOptions) => Promise<T>;

function serializeBody(body: unknown): BodyInit | undefined {
  if (body === undefined) {
    return undefined;
  }

  if (typeof body === "string" || body instanceof FormData || body instanceof Blob) {
    return body;
  }

  return JSON.stringify(body);
}

/** Builds a URL with URLSearchParams so each query value is encoded exactly once. */
export function buildApiUrl(input: string | URL, query?: ApiQuery): string {
  if (!query || Object.keys(query).length === 0) {
    return input.toString();
  }

  const url = input.toString();
  const separatorIndex = url.indexOf("?");
  const path = separatorIndex >= 0 ? url.slice(0, separatorIndex) : url;
  const existingQuery = separatorIndex >= 0 ? url.slice(separatorIndex + 1) : "";
  const params = new URLSearchParams(existingQuery);

  for (const [key, value] of Object.entries(query)) {
    const values = Array.isArray(value) ? value : [value];
    for (const item of values) {
      if (item !== null && item !== undefined) {
        params.append(key, String(item));
      }
    }
  }

  const serialized = params.toString();
  return serialized ? `${path}?${serialized}` : path;
}

export type ApiClient = {
  get<T>(input: RequestInfo | URL, options?: ApiClientRequestOptions): Promise<T>;
  post<T>(input: RequestInfo | URL, body?: unknown, options?: ApiClientRequestOptions): Promise<T>;
  put<T>(input: RequestInfo | URL, body?: unknown, options?: ApiClientRequestOptions): Promise<T>;
  patch<T>(input: RequestInfo | URL, body?: unknown, options?: ApiClientRequestOptions): Promise<T>;
  delete<T>(input: RequestInfo | URL, options?: ApiClientRequestOptions): Promise<T>;
  query<T>(input: RequestInfo | URL, query: ApiQuery, options?: ApiClientRequestOptions): Promise<T>;
};

export function createApiClient(transport: ApiTransport): ApiClient {
  function request<T>(
    method: HttpMethod,
    input: RequestInfo | URL,
    body?: unknown,
    options?: ApiClientRequestOptions
  ): Promise<T> {
    const serializedBody = serializeBody(body);

    return transport<T>(input, {
      ...options,
      method,
      ...(serializedBody === undefined ? {} : { body: serializedBody })
    });
  }

  const client: ApiClient = {
    get<T>(input: RequestInfo | URL, options?: ApiClientRequestOptions): Promise<T> {
      return request<T>(HTTP_METHODS.GET, input, undefined, options);
    },

    post<T>(
      input: RequestInfo | URL,
      body?: unknown,
      options?: ApiClientRequestOptions
    ): Promise<T> {
      return request<T>(HTTP_METHODS.POST, input, body, options);
    },

    put<T>(
      input: RequestInfo | URL,
      body?: unknown,
      options?: ApiClientRequestOptions
    ): Promise<T> {
      return request<T>(HTTP_METHODS.PUT, input, body, options);
    },

    patch<T>(
      input: RequestInfo | URL,
      body?: unknown,
      options?: ApiClientRequestOptions
    ): Promise<T> {
      return request<T>(HTTP_METHODS.PATCH, input, body, options);
    },

    delete<T>(input: RequestInfo | URL, options?: ApiClientRequestOptions): Promise<T> {
      return request<T>(HTTP_METHODS.DELETE, input, undefined, options);
    },

    query<T>(
      input: RequestInfo | URL,
      query: ApiQuery,
      options?: ApiClientRequestOptions
    ): Promise<T> {
      return client.get<T>(buildApiUrl(input.toString(), query), options);
    }
  };

  return client;
}
