import { describe, expect, test } from "bun:test";
import { buildApiUrl, createApiClient } from "../src/shared/api/apiClient";

describe("api client query builder", () => {
  test("encodes raw filter values once and decodes back to the original value", () => {
    const filter = "((Code contains `x`) | (OriginalUrl contains `x`))";
    const url = buildApiUrl("/api/search", { filter });

    expect(url).toContain("%28%28Code+contains+%60x%60%29+%7C+%28OriginalUrl+contains+%60x%60%29%29");
    expect(url).not.toContain("%2528");
    expect(new URL(url, "http://localhost").searchParams.get("filter")).toBe(filter);
  });

  test("preserves existing query values and supports repeated values", () => {
    const url = buildApiUrl("/api/search?include=archived", {
      page: 2,
      tags: ["one", "two"],
      optional: undefined
    });
    const params = new URL(url, "http://localhost").searchParams;

    expect(params.get("include")).toBe("archived");
    expect(params.get("page")).toBe("2");
    expect(params.getAll("tags")).toEqual(["one", "two"]);
    expect(params.has("optional")).toBe(false);
  });

  test("routes every verb through one transport and serializes JSON bodies", async () => {
    const requests: RequestInit[] = [];
    const client = createApiClient(async (_input, init) => {
      requests.push(init ?? {});
      return undefined as never;
    });

    await client.get("/api/items");
    await client.post("/api/items", { name: "demo" });
    await client.put("/api/items/1", { name: "updated" });
    await client.patch("/api/items/1", { enabled: true });
    await client.delete("/api/items/1");
    await client.query("/api/items", { page: 2 });

    expect(requests.map((request) => request.method)).toEqual([
      "GET",
      "POST",
      "PUT",
      "PATCH",
      "DELETE",
      "GET"
    ]);
    expect(requests[1].body).toBe(JSON.stringify({ name: "demo" }));
    expect(requests[5].signal).toBeUndefined();
  });
});
