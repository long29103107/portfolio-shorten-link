import type { ShortLinkFormInput } from "../types";

export type ShortLinkFieldErrors = Partial<Record<keyof ShortLinkFormInput, string>>;

export function validateShortLinkForm(
  form: ShortLinkFormInput,
  now = new Date()
): ShortLinkFieldErrors {
  const errors: ShortLinkFieldErrors = {};
  const originalUrl = form.originalUrl.trim();

  if (!originalUrl) {
    errors.originalUrl = "Paste a full destination URL to shorten.";
  } else {
    try {
      const url = new URL(originalUrl);
      if (url.protocol !== "http:" && url.protocol !== "https:") {
        errors.originalUrl = "Use an http:// or https:// link.";
      }
    } catch {
      errors.originalUrl = "The destination URL does not look valid yet.";
    }
  }

  if (!form.expiredAtLocal) {
    errors.expiredAtLocal = "Choose an expiry time.";
  } else {
    const expiry = new Date(form.expiredAtLocal);
    if (Number.isNaN(expiry.getTime()) || expiry.getTime() <= now.getTime()) {
      errors.expiredAtLocal = "Choose an expiry time in the future.";
    }

    if (form.activeFromLocal) {
      const activeFrom = new Date(form.activeFromLocal);
      if (Number.isNaN(activeFrom.getTime())) {
        errors.activeFromLocal = "Choose a valid start time.";
      } else if (!errors.expiredAtLocal && activeFrom.getTime() >= expiry.getTime()) {
        errors.activeFromLocal = "Start time must be earlier than expiry.";
        errors.expiredAtLocal = "Expiry must be later than the start time.";
      }
    }
  }

  if (form.activeFromLocal && !errors.activeFromLocal) {
    const activeFrom = new Date(form.activeFromLocal);
    if (Number.isNaN(activeFrom.getTime())) {
      errors.activeFromLocal = "Choose a valid start time.";
    }
  }

  return errors;
}

export function mapShortLinkApiFieldErrors(
  fieldErrors: Record<string, string>
): ShortLinkFieldErrors {
  const errors: ShortLinkFieldErrors = {};

  if (fieldErrors.originalUrl) {
    errors.originalUrl = fieldErrors.originalUrl;
  }

  if (fieldErrors.expiredAtUtc) {
    errors.expiredAtLocal = fieldErrors.expiredAtUtc;
  }

  if (fieldErrors.activeFromUtc) {
    errors.activeFromLocal = fieldErrors.activeFromUtc;
  }

  return errors;
}

export function hasShortLinkFieldErrors(errors: ShortLinkFieldErrors): boolean {
  return Boolean(errors.originalUrl || errors.activeFromLocal || errors.expiredAtLocal);
}
