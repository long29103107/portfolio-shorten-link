import type { ShortLinkFormInput } from "../types";
import { parseTagInput } from "./organization";

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

  if (form.maxClicksLocal.trim()) {
    const maxClicks = Number(form.maxClicksLocal);
    if (!Number.isInteger(maxClicks) || maxClicks <= 0) {
      errors.maxClicksLocal = "Enter a positive whole-number click limit, or leave it blank for unlimited clicks.";
    }
  }

  if (form.activeFromLocal && !errors.activeFromLocal) {
    const activeFrom = new Date(form.activeFromLocal);
    if (Number.isNaN(activeFrom.getTime())) {
      errors.activeFromLocal = "Choose a valid start time.";
    }
  }

  const password = form.passwordLocal ?? "";
  if (password.length > 256) {
    errors.passwordLocal = "Enter a non-empty password of 256 characters or fewer.";
  }

  if ((form.folderLocal ?? "").trim().length > 128) {
    errors.folderLocal = "Folder must be 128 characters or fewer.";
  }

  const tags = parseTagInput(form.tagsLocal ?? "");
  if (tags.length > 20 || tags.some((tag) => tag.length > 64)) {
    errors.tagsLocal = "Use up to 20 tags, with each tag 64 characters or fewer.";
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

  if (fieldErrors.maxClicks) {
    errors.maxClicksLocal = fieldErrors.maxClicks;
  }

  if (fieldErrors.password) {
    errors.passwordLocal = fieldErrors.password;
  }

  if (fieldErrors.folder) {
    errors.folderLocal = fieldErrors.folder;
  }

  if (fieldErrors.tags) {
    errors.tagsLocal = fieldErrors.tags;
  }

  return errors;
}

export function hasShortLinkFieldErrors(errors: ShortLinkFieldErrors): boolean {
  return Boolean(errors.originalUrl || errors.activeFromLocal || errors.expiredAtLocal || errors.maxClicksLocal || errors.passwordLocal || errors.folderLocal || errors.tagsLocal);
}
