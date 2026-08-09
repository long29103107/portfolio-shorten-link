export const EXPIRING_SOON_THRESHOLD_MS = 24 * 60 * 60 * 1000;

export const EXPIRY_PRESETS = [
  { label: "+30m", minutes: 30 },
  { label: "+60m", minutes: 60 },
  { label: "+180m", minutes: 180 },
  { label: "+6h", minutes: 6 * 60 },
  { label: "+12h", minutes: 12 * 60 }
] as const;

export function createExpiryPresetValue(referenceTime: Date, minutes: number): string {
  return toDateTimeLocalValue(new Date(referenceTime.getTime() + minutes * 60_000));
}

export function toDateTimeLocalValue(date: Date): string {
  const offsetMs = date.getTimezoneOffset() * 60_000;
  return new Date(date.getTime() - offsetMs).toISOString().slice(0, 16);
}

export type ExpiryLifecycleState =
  | "deleted"
  | "inactive"
  | "scheduled"
  | "expired"
  | "expiring-soon"
  | "active"
  | "unknown";

export type ExpiryPresentationInput = {
  expiredAtUtc: string | null;
  activeFromUtc?: string | null;
  isActive: boolean;
  isDeleted?: boolean;
};

export type ExpiryPresentation = {
  state: ExpiryLifecycleState;
  label: string;
  detail: string;
  dateTime: string;
};

export function getExpiryPresentation(
  input: ExpiryPresentationInput,
  referenceTime: Date,
  soonThresholdMs = EXPIRING_SOON_THRESHOLD_MS
): ExpiryPresentation {
  const dateTime = formatExpiryDateTime(input.expiredAtUtc);

  if (input.isDeleted) {
    return { state: "deleted", label: "Deleted", detail: "This link is deleted.", dateTime };
  }

  if (!input.isActive) {
    return { state: "inactive", label: "Inactive", detail: "This link is inactive.", dateTime };
  }

  const activeFrom = input.activeFromUtc ? new Date(input.activeFromUtc) : null;
  const reference = referenceTime.getTime();
  if (activeFrom && !Number.isNaN(activeFrom.getTime()) && activeFrom.getTime() > reference) {
    return {
      state: "scheduled",
      label: "Scheduled",
      detail: `Starts at ${formatExpiryDateTime(input.activeFromUtc ?? null)}.`,
      dateTime
    };
  }

  if (!input.expiredAtUtc) {
    return { state: "unknown", label: "No expiry", detail: "No expiry is configured.", dateTime };
  }

  const expiry = new Date(input.expiredAtUtc);
  if (Number.isNaN(expiry.getTime()) || Number.isNaN(reference)) {
    return { state: "unknown", label: "Expiry unavailable", detail: "The expiry time could not be read.", dateTime };
  }

  const remainingMs = expiry.getTime() - reference;
  if (remainingMs <= 0) {
    return { state: "expired", label: "Expired", detail: "This link has expired.", dateTime };
  }

  if (remainingMs <= soonThresholdMs) {
    return {
      state: "expiring-soon",
      label: "Expiring soon",
      detail: `Expires within the next ${formatThresholdHours(soonThresholdMs)} hours.`,
      dateTime
    };
  }

  return { state: "active", label: "Active", detail: "This link is active.", dateTime };
}

function formatThresholdHours(thresholdMs: number): string {
  const hours = thresholdMs / (60 * 60 * 1000);
  return Number.isInteger(hours) ? String(hours) : hours.toFixed(1);
}

export function formatExpiryDateTime(value: string | null): string {
  if (!value) {
    return "No expiry";
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime())
    ? value
    : new Intl.DateTimeFormat(undefined, {
        year: "numeric",
        month: "short",
        day: "numeric",
        hour: "numeric",
        minute: "2-digit",
        timeZoneName: "short"
      }).format(date);
}
