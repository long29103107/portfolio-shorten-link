import { describe, expect, test } from "bun:test";
import { buildShortLinkListUrl, buildShortLinkQueryParams } from "../src/features/short-links/api/shortLinksApi";
import {
  createShortLinkDiscoveryChange,
  defaultShortLinkDiscoveryQuery,
  hasShortLinkDiscoveryCriteria
} from "../src/features/short-links/components/ShortLinkDiscoveryToolbar";
import { isCurrentRequestGeneration } from "../src/features/short-links/domain/requestLifecycle";

describe("admin discovery", () => {
  test("serializes supported list discovery parameters", () => {
    expect(buildShortLinkListUrl(10, 3, {
      search: "  docs.example  ",
      status: "all",
      sortBy: "destination",
      sortDirection: "asc"
    })).toBe(
      "/api/short-links?limit=10&page=3&fe=%28%28Code+contains+%60docs.example%60%29+%7C+%28OriginalUrl+contains+%60docs.example%60%29%29&sort=%2BOriginalUrl"
    );
  });

  test("encodes the filter once and exposes the original value through URLSearchParams", () => {
    const url = buildShortLinkListUrl(25, 1, {
      search: "x",
      status: "all",
      sortBy: "created",
      sortDirection: "desc"
    });
    const filter = "((Code contains `x`) | (OriginalUrl contains `x`))";
    const encoded = new URL(`http://localhost${url}`).searchParams.get("fe");

    expect(encoded).toBe(filter);
    expect(url).not.toContain("%2528");
  });

  test("builds filter and sort through the fe and sort query keys", () => {
    const params = buildShortLinkQueryParams(25, 1, {
      search: "chatgpt",
      status: "all",
      sortBy: "created",
      sortDirection: "desc"
    });

    expect(params.get("fe")).toBe("((Code contains `chatgpt`) | (OriginalUrl contains `chatgpt`))");
    expect(params.get("sort")).toBe("-CreatedAt");
    expect(params.toString()).not.toContain("%2528");
  });

  test("omits an empty search while preserving explicit defaults", () => {
    expect(buildShortLinkListUrl(25, 1, defaultShortLinkDiscoveryQuery)).toBe(
      "/api/short-links?limit=25&page=1&sort=-CreatedAt"
    );
    expect(hasShortLinkDiscoveryCriteria(defaultShortLinkDiscoveryQuery)).toBe(false);
  });

  test("resets numbered pagination when toolbar criteria change", () => {
    const nextQuery = { ...defaultShortLinkDiscoveryQuery, status: "inactive" as const };
    expect(createShortLinkDiscoveryChange(nextQuery)).toEqual({
      query: nextQuery,
      pageNumber: 1
    });
    expect(hasShortLinkDiscoveryCriteria(nextQuery)).toBe(true);
  });

  test("accepts only the current non-aborted discovery request generation", () => {
    const controller = new AbortController();

    expect(isCurrentRequestGeneration(2, 2, controller.signal)).toBe(true);
    expect(isCurrentRequestGeneration(1, 2, controller.signal)).toBe(false);

    controller.abort();
    expect(isCurrentRequestGeneration(2, 2, controller.signal)).toBe(false);
  });
});
