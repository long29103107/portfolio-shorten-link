import { describe, expect, test } from "bun:test";
import { listShortLinkShares } from "../src/features/short-links/api/shortLinksApi";
import { isCurrentRequestGeneration } from "../src/features/short-links/domain/requestLifecycle";

describe("share discovery lifecycle", () => {
  test("keeps share reads tied to the current non-aborted generation", () => {
    const controller = new AbortController();

    expect(isCurrentRequestGeneration(3, 3, controller.signal)).toBe(true);
    expect(isCurrentRequestGeneration(2, 3, controller.signal)).toBe(false);

    controller.abort();
    expect(isCurrentRequestGeneration(3, 3, controller.signal)).toBe(false);
  });

  test("passes the cancellation signal through the share list API", async () => {
    const globalState = globalThis as typeof globalThis & { window?: Window };
    const originalFetch = globalThis.fetch;
    const originalWindow = globalState.window;
    const controller = new AbortController();
    let receivedSignal: AbortSignal | undefined;

    globalState.window = {
      localStorage: {
        getItem: () => null
      }
    } as unknown as Window;
    globalThis.fetch = async (_input, init) => {
      receivedSignal = init?.signal;
      return new Response(JSON.stringify({ mode: "AllowList", items: [] }), {
        status: 200,
        headers: { "content-type": "application/json" }
      });
    };

    try {
      await listShortLinkShares("abc1234", controller.signal);
      expect(receivedSignal).toBe(controller.signal);
    } finally {
      globalThis.fetch = originalFetch;
      if (originalWindow) {
        globalState.window = originalWindow;
      } else {
        delete globalState.window;
      }
    }
  });
});
