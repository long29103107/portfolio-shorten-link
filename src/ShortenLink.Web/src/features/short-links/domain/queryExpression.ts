import { filter, type FilterExpression, type SortExpression } from "../../../shared/queryExpression";
import type { ShortLinkDiscoveryQuery } from "../types";

export type ShortLinkQueryField = "Code" | "OriginalUrl" | "CreatedAt" | "ExpiresAt" | "IsActive";

const sortFields: Record<Exclude<ShortLinkDiscoveryQuery["sortBy"], "status">, ShortLinkQueryField> = {
  created: "CreatedAt",
  expiry: "ExpiresAt",
  destination: "OriginalUrl",
  code: "Code"
};

export function buildShortLinkFilterExpression(
  query: ShortLinkDiscoveryQuery,
  now = new Date()
): FilterExpression | undefined {
  const search = query.search.trim();
  const expressions: FilterExpression[] = [];
  if (search) {
    expressions.push(filter.or(
      filter.condition("Code", "contains", search),
      filter.condition("OriginalUrl", "contains", search)
    ));
  }
  if (query.status === "inactive") {
    expressions.push(filter.condition("IsActive", "eq", false));
  } else if (query.status === "active") {
    expressions.push(filter.and(
      filter.condition("IsActive", "eq", true),
      filter.or(
        filter.condition("ExpiresAt", "eq", "null"),
        filter.condition("ExpiresAt", "gt", now)
      )
    ));
  } else if (query.status === "expired") {
    expressions.push(filter.and(
      filter.condition("IsActive", "eq", true),
      filter.condition("ExpiresAt", "le", now)
    ));
  } else if (query.status === "expiring-soon") {
    const expiringSoonBefore = new Date(now.getTime() + 7 * 24 * 60 * 60 * 1000);
    expressions.push(filter.and(
      filter.condition("IsActive", "eq", true),
      filter.condition("ExpiresAt", "gt", now),
      filter.condition("ExpiresAt", "le", expiringSoonBefore)
    ));
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
