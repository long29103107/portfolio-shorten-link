import type {
  AuditLogEvent,
  AuditLogFilters,
  AuditLogQuery
} from "./types";

export const emptyAuditLogFilters: AuditLogFilters = {
  action: "",
  targetId: "",
  actorId: "",
  from: "",
  to: ""
};

export function buildAuditLogUrl(query: AuditLogQuery = {}): string {
  const params = new URLSearchParams({
    limit: String(Math.min(Math.max(query.limit ?? 50, 1), 200))
  });

  if (query.cursor?.trim()) {
    params.set("cursor", query.cursor.trim());
  }

  const filters = query.filters ?? emptyAuditLogFilters;
  setTrimmed(params, "action", filters.action);
  setTrimmed(params, "targetId", filters.targetId);
  setTrimmed(params, "actorId", filters.actorId);
  setTrimmed(params, "from", filters.from);
  setTrimmed(params, "to", filters.to);

  return `/api/audit-logs?${params.toString()}`;
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

function setTrimmed(params: URLSearchParams, key: string, value: string) {
  const normalized = value.trim();
  if (normalized) {
    params.set(key, normalized);
  }
}
