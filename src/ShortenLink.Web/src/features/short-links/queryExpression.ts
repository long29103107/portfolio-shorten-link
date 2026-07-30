import { filter, type FilterExpression, type SortExpression } from "../../shared/queryExpression";
import type { ShortLinkDiscoveryQuery } from "./types";

export type ShortLinkQueryField = "Code" | "OriginalUrl" | "CreatedAt" | "ExpiresAt" | "IsActive";

const sortFields: Record<Exclude<ShortLinkDiscoveryQuery["sortBy"], "status">, ShortLinkQueryField> = {
  created: "CreatedAt",
  expiry: "ExpiresAt",
  destination: "OriginalUrl",
  code: "Code"
};

export function buildShortLinkFilterExpression(query: ShortLinkDiscoveryQuery): FilterExpression | undefined {
  const search = query.search.trim();
  const expressions: FilterExpression[] = [];
  if (search) {
    expressions.push(filter.or(
      filter.condition("Code", "contains", search),
      filter.condition("OriginalUrl", "contains", search)
    ));
  }
  if (query.status === "active" || query.status === "inactive") {
    expressions.push(filter.condition("IsActive", "eq", query.status === "active"));
  }
  if (expressions.length === 0) return undefined;
  return expressions.length === 1 ? expressions[0] : filter.and(...expressions);
}

export function buildShortLinkSortExpression(query: ShortLinkDiscoveryQuery): SortExpression<ShortLinkQueryField>[] {
  if (query.sortBy === "status") return [];
  return [{ field: sortFields[query.sortBy], direction: query.sortDirection }];
}

export function toggleShortLinkSort(
  query: ShortLinkDiscoveryQuery,
  sortBy: ShortLinkDiscoveryQuery["sortBy"]
): ShortLinkDiscoveryQuery {
  return {
    ...query,
    sortBy,
    sortDirection: query.sortBy === sortBy && query.sortDirection === "asc" ? "desc" : "asc"
  };
}
