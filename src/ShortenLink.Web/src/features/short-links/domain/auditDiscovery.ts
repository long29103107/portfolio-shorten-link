import type {
  AuditLogEvent,
  AuditLogFilters,
  AuditLogQuery
} from "../types";
import { appendQueryExpression, filter, type FilterExpression } from "../../../shared/queryExpression";
import { SHORT_LINK_API_ROUTES } from "../constants/apiRoutes";
import { AUDIT_LOG_DEFAULTS } from "../constants/defaults";

export const emptyAuditLogFilters: AuditLogFilters = {
  action: "",
  targetId: "",
  actorId: "",
  from: "",
  to: ""
};

export function buildAuditLogUrl(query: AuditLogQuery = {}): string {
  const params = new URLSearchParams({
    limit: String(Math.min(
      Math.max(query.limit ?? AUDIT_LOG_DEFAULTS.LIMIT, AUDIT_LOG_DEFAULTS.MIN_LIMIT),
      AUDIT_LOG_DEFAULTS.MAX_LIMIT
    ))
  });

  if (query.cursor?.trim()) {
    params.set("cursor", query.cursor.trim());
  }

  const filters = { ...emptyAuditLogFilters, ...(query.filters ?? {}) };
  const expressions: FilterExpression[] = [];
  addCondition(expressions, "Action", "eq", filters.action);
  addCondition(expressions, "TargetId", "eq", filters.targetId);
  addCondition(expressions, "ActorId", "eq", filters.actorId);
  addCondition(expressions, "OccurredAt", "ge", filters.from);
  addCondition(expressions, "OccurredAt", "le", filters.to);
  appendQueryExpression(params, {
    filter: expressions.length === 0
      ? undefined
      : expressions.length === 1
        ? expressions[0]
        : filter.and(...expressions)
  });

  return `${SHORT_LINK_API_ROUTES.AUDIT_LOGS}?${params.toString()}`;
}

export function mergeAuditLogEvents(
  current: AuditLogEvent[],
  incoming: AuditLogEvent[]
): AuditLogEvent[] {
  const seen = new Set(current.map((event) => event.id));
  return [
    ...current,
    ...incoming.filter((event) => {
      if (seen.has(event.id)) {
        return false;
      }
      seen.add(event.id);
      return true;
    })
  ];
}

export function toAuditFilterIso(localValue: string): string {
  const trimmed = localValue.trim();
  if (!trimmed) {
    return "";
  }

  const date = new Date(trimmed);
  return Number.isNaN(date.getTime()) ? "" : date.toISOString();
}

export function validateAuditTimeRange(fromLocal: string, toLocal: string): string | null {
  if (!fromLocal || !toLocal) {
    return null;
  }

  const from = new Date(fromLocal);
  const to = new Date(toLocal);
  if (Number.isNaN(from.getTime()) || Number.isNaN(to.getTime())) {
    return "Enter a valid time range.";
  }

  return from > to ? "From must be earlier than or equal to To." : null;
}

export function formatAuditLabel(value: string): string {
  return value
    .split(/[._]/)
    .filter(Boolean)
    .map((part) => part[0]?.toUpperCase() + part.slice(1))
    .join(" ");
}

function addCondition(
  expressions: FilterExpression[],
  field: string,
  operator: "eq" | "ge" | "le",
  value: string
) {
  const normalized = value.trim();
  if (normalized) {
    expressions.push(filter.condition(field, operator, normalized));
  }
}
