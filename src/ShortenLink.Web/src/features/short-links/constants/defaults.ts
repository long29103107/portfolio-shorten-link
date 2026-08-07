export const SHORT_LINK_DISCOVERY_DEFAULTS: Record<"LIMIT" | "PAGE", number> = {
  LIMIT: 25,
  PAGE: 1
} as const;

export const AUDIT_LOG_DEFAULTS: Record<"LIMIT" | "MIN_LIMIT" | "MAX_LIMIT", number> = {
  LIMIT: 50,
  MIN_LIMIT: 1,
  MAX_LIMIT: 200
} as const;

export const DASHBOARD_DEFAULTS: Record<"LINK_LIMIT" | "RECENT_LINK_LIMIT" | "RECENT_REJECTION_LIMIT", number> = {
  LINK_LIMIT: 1,
  RECENT_LINK_LIMIT: 6,
  RECENT_REJECTION_LIMIT: 5
} as const;
