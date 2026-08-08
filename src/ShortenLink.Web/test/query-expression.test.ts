import { describe, expect, test } from "bun:test";
import { filter, serializeFilter, serializeSort } from "../src/shared/queryExpression";
import { buildShortLinkFilterExpression, toggleShortLinkSort } from "../src/features/short-links/domain/queryExpression";

describe("query expression", () => {
  test("serializes nested filter groups", () => {
    expect(serializeFilter(filter.or(
      filter.and(filter.condition("Score", "ge", 20), filter.condition("Name", "startsWith", "Al")),
      filter.condition("Name", "eq", "Beta")
    ))).toBe("(((Score ge `20`) & (Name startsWith `Al`)) | (Name eq `Beta`))");
  });

  test("serializes not, in, and multiple sort fields", () => {
    expect(serializeFilter(filter.not(filter.condition("Id", "in", [1, 3])))).toBe("!(Id in `[1,3]`)");
    expect(serializeSort([{ field: "Score", direction: "desc" }, { field: "Name" }])).toBe("-Score,+Name");
  });

  test("rejects unsafe fields and unsupported backticks", () => {
    expect(() => serializeFilter(filter.condition("Name);drop", "eq", "x"))).toThrow();
    expect(() => serializeFilter(filter.condition("Name", "eq", "bad`value"))).toThrow();
  });

  test("toggles table sorting and starts a different column ascending", () => {
    const query = { search: "", status: "all", sortBy: "created", sortDirection: "desc" } as const;
    expect(toggleShortLinkSort(query, "created").sortDirection).toBe("asc");
    expect(toggleShortLinkSort({ ...query, sortDirection: "asc" }, "created").sortDirection).toBe("desc");
    expect(toggleShortLinkSort(query, "code")).toMatchObject({ sortBy: "code", sortDirection: "asc" });
  });

  test("moves derived status filters into the filter expression", () => {
    const expression = buildShortLinkFilterExpression(
      { search: "", status: "expiring-soon", sortBy: "created", sortDirection: "desc" },
      new Date("2026-07-15T12:00:00.000Z")
    );

    expect(serializeFilter(expression!)).toBe(
      "((IsActive eq `true`) & (ExpiresAt gt `2026-07-15T12:00:00.000Z`) & (ExpiresAt le `2026-07-22T12:00:00.000Z`))"
    );
  });
});
