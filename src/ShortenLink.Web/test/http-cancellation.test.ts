import { expect, test } from "bun:test";
import { fetchJson } from "../src/features/short-links/api/http";

test("does not emit a failure toast when fetch is aborted", async () => {
  const globalState = globalThis as typeof globalThis & {
    window?: Window;
  };
  const originalFetch = globalThis.fetch;
  const originalWindow = globalState.window;
  const abortError = Object.assign(new Error("The request was aborted."), { name: "AbortError" });
  let dispatchedEvents = 0;

  globalState.window = {
    localStorage: {
      getItem: () => null
    },
    dispatchEvent: () => {
      dispatchedEvents += 1;
      return true;
    }
  } as unknown as Window;
  globalThis.fetch = async () => {
    throw abortError;
  };

  try {
    await expect(fetchJson("/api/short-links", {
      signal: new AbortController().signal
    })).rejects.toBe(abortError);
    expect(dispatchedEvents).toBe(0);
  } finally {
    globalThis.fetch = originalFetch;
    if (originalWindow) {
      globalState.window = originalWindow;
    } else {
      delete globalState.window;
    }
  }
});
