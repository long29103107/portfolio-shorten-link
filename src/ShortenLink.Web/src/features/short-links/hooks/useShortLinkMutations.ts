import { useState, type Dispatch, type SetStateAction } from "react";
import {
  activateShortLink,
  createShortLink,
  deactivateShortLink,
  deleteShortLink,
  updateShortLink
} from "../api/shortLinksApi";
import { ApiError } from "../api/http";
import type { AdminPermissionState } from "../api/adminSecurity";
import type {
  ShortLinkAdminItem,
  ShortLinkAdminPageResult,
  ShortLinkFormInput
} from "../types";
import { toFriendlyErrorMessage } from "../types";
import { toDateTimeLocalValue as formatDateTimeLocal } from "../domain/expiryPresentation";
import {
  hasShortLinkFieldErrors,
  mapShortLinkApiFieldErrors,
  type ShortLinkFieldErrors,
  validateShortLinkForm
} from "../domain/validation";
import { shouldPreserveMutationContext } from "@/shared/api/recovery";
import { showToast } from "@/shared/toast";

type UseShortLinkMutationsOptions = {
  adminPermissions: AdminPermissionState;
  links: ShortLinkAdminItem[];
  setLinks: Dispatch<SetStateAction<ShortLinkAdminItem[]>>;
  loadLinks: (nextPageNumber?: number) => Promise<ShortLinkAdminPageResult | null>;
  selectedCodes: Set<string>;
  setSelectedCodes: Dispatch<SetStateAction<Set<string>>>;
  onCloseMenu: () => void;
  analyticsCode: string | null;
  onAnalyticsClose: () => void;
};

const emptyEditForm: ShortLinkFormInput = {
  originalUrl: "",
  activeFromLocal: "",
  expiredAtLocal: "",
  maxClicksLocal: "",
  passwordLocal: "",
  clearPassword: false
};

export function buildShortLinkMutationPayload(form: ShortLinkFormInput) {
  const password = form.passwordLocal ?? "";
  return {
    originalUrl: form.originalUrl.trim(),
    activeFromUtc: form.activeFromLocal ? new Date(form.activeFromLocal).toISOString() : null,
    expiredAtUtc: new Date(form.expiredAtLocal).toISOString(),
    maxClicks: form.maxClicksLocal.trim() ? Number(form.maxClicksLocal) : null,
    ...(password.trim() ? { password } : {}),
    ...(form.clearPassword ? { clearPassword: true } : {})
  };
}

export function useShortLinkMutations({
  adminPermissions,
  links,
  setLinks,
  loadLinks,
  selectedCodes,
  setSelectedCodes,
  onCloseMenu,
  analyticsCode,
  onAnalyticsClose
}: UseShortLinkMutationsOptions) {
  const [actionError, setActionError] = useState<string | null>(null);
  const [busyCode, setBusyCode] = useState<string | null>(null);
  const [isBulkDeleting, setIsBulkDeleting] = useState(false);
  const [isBulkUpdating, setIsBulkUpdating] = useState(false);
  const [isCreating, setIsCreating] = useState(false);
  const [editingCode, setEditingCode] = useState<string | null>(null);
  const [editForm, setEditForm] = useState<ShortLinkFormInput>(emptyEditForm);
  const [initialEditForm, setInitialEditForm] = useState<ShortLinkFormInput>(emptyEditForm);
  const [fieldErrors, setFieldErrors] = useState<ShortLinkFieldErrors>({});
  const [editorRequestError, setEditorRequestError] = useState<string | null>(null);

  const editingLink = editingCode
    ? links.find((link) => link.code === editingCode) ?? null
    : null;
  const isEditorOpen = isCreating || editingLink !== null;
  const hasEditChanges = isEditorOpen
    && (editForm.originalUrl !== initialEditForm.originalUrl
      || editForm.activeFromLocal !== initialEditForm.activeFromLocal
      || editForm.expiredAtLocal !== initialEditForm.expiredAtLocal
      || editForm.maxClicksLocal !== initialEditForm.maxClicksLocal
      || editForm.passwordLocal !== initialEditForm.passwordLocal
      || editForm.clearPassword !== initialEditForm.clearPassword);

  const handleDeactivate = async (code: string) => {
    setBusyCode(code);
    setActionError(null);

    try {
      const response = await deactivateShortLink(code);
      setLinks((current) =>
        current.map((link) =>
          link.code === response.code ? { ...link, isActive: response.isActive } : link
        )
      );
      showToast({
        title: "Short link deactivated",
        message: code,
        variant: "success"
      });
    } catch (error) {
      setActionError(error instanceof ApiError
        ? toFriendlyErrorMessage(error.errorCode, error.message)
        : "The link could not be deactivated.");
    } finally {
      setBusyCode(null);
    }
  };

  const handleActivate = async (code: string) => {
    setBusyCode(code);
    setActionError(null);

    try {
      const response = await activateShortLink(code);
      setLinks((current) =>
        current.map((link) =>
          link.code === response.code ? { ...link, isActive: response.isActive } : link
        )
      );
      showToast({
        title: "Short link activated",
        message: code,
        variant: "success"
      });
    } catch (error) {
      setActionError(error instanceof ApiError
        ? toFriendlyErrorMessage(error.errorCode, error.message)
        : "The link could not be activated.");
    } finally {
      setBusyCode(null);
    }
  };

  const startEdit = (link: ShortLinkAdminItem) => {
    if (!adminPermissions.canUpdate) return;

    setIsCreating(false);
    setEditingCode(link.code);
    onCloseMenu();
    setActionError(null);
    setEditorRequestError(null);
    setFieldErrors({});
    const nextForm = {
      originalUrl: link.originalUrl,
      activeFromLocal: toEditorExpiryValue(link.activeFromUtc),
      expiredAtLocal: toEditorExpiryValue(link.expiredAtUtc),
      maxClicksLocal: link.maxClicks === null ? "" : String(link.maxClicks),
      passwordLocal: "",
      clearPassword: false
    };
    setEditForm(nextForm);
    setInitialEditForm(nextForm);
  };

  const startCreate = () => {
    if (!adminPermissions.canCreate) return;

    setIsCreating(true);
    setEditingCode(null);
    onCloseMenu();
    setActionError(null);
    setEditorRequestError(null);
    setFieldErrors({});
    setEditForm(emptyEditForm);
    setInitialEditForm(emptyEditForm);
  };

  const closeEditor = () => {
    setIsCreating(false);
    setEditingCode(null);
    setFieldErrors({});
    setInitialEditForm(emptyEditForm);
    setEditorRequestError(null);
  };

  const applyApiFieldError = (error: ApiError) => {
    const nextErrors = mapShortLinkApiFieldErrors(error.fieldErrors);
    setFieldErrors(nextErrors);
    return hasShortLinkFieldErrors(nextErrors);
  };

  const handleCreate = async () => {
    const nextErrors = validateShortLinkForm(editForm);
    if (hasShortLinkFieldErrors(nextErrors)) {
      setFieldErrors(nextErrors);
      return;
    }
    setFieldErrors({});

    const payload = buildShortLinkMutationPayload(editForm);

    setBusyCode("__create__");
    setActionError(null);
    setEditorRequestError(null);

    try {
      const created = await createShortLink({
        ...payload,
        password: payload.password ?? null
      });
      closeEditor();
      await loadLinks(1);
      showToast({
        title: "Short link created",
        message: created.code,
        variant: "success"
      });
    } catch (error) {
      if (error instanceof ApiError && applyApiFieldError(error)) return;

      const message = error instanceof ApiError
        ? toFriendlyErrorMessage(error.errorCode, error.message)
        : "The link could not be created.";

      if (shouldPreserveMutationContext(error)) {
        setEditorRequestError(message);
        return;
      }

      closeEditor();
      showToast({ title: "Create failed", message, variant: "error" });
    } finally {
      setBusyCode(null);
    }
  };

  const handleSaveEdit = async (code: string) => {
    const nextErrors = validateShortLinkForm(editForm);
    if (hasShortLinkFieldErrors(nextErrors)) {
      setFieldErrors(nextErrors);
      return;
    }
    setFieldErrors({});

    const payload = buildShortLinkMutationPayload(editForm);

    setBusyCode(code);
    setActionError(null);
    setEditorRequestError(null);

    try {
      const updated = await updateShortLink(code, {
        ...payload,
        password: payload.password ?? null,
        clearPassword: payload.clearPassword ?? false
      });
      setLinks((current) =>
        current.map((link) => (
          link.code === updated.code
            ? { ...updated, accessLevel: updated.accessLevel ?? link.accessLevel }
            : link
        ))
      );
      closeEditor();
      showToast({ title: "Short link updated", message: code, variant: "success" });
    } catch (error) {
      if (error instanceof ApiError && applyApiFieldError(error)) return;

      const message = error instanceof ApiError
        ? toFriendlyErrorMessage(error.errorCode, error.message)
        : "The link could not be updated.";

      if (shouldPreserveMutationContext(error)) {
        setEditorRequestError(message);
        return;
      }

      closeEditor();
      showToast({ title: "Update failed", message, variant: "error" });
    } finally {
      setBusyCode(null);
    }
  };

  const handleDelete = async (code: string) => {
    setBusyCode(code);
    setActionError(null);

    try {
      const response = await deleteShortLink(code);
      setLinks((current) => current.filter((link) => link.code !== response.code));
      setSelectedCodes((current) => {
        const next = new Set(current);
        next.delete(response.code);
        return next;
      });
      if (editingCode === response.code) setEditingCode(null);
      if (analyticsCode === response.code) onAnalyticsClose();
      showToast({ title: "Short link deleted", message: response.code, variant: "success" });
    } catch (error) {
      setActionError(error instanceof ApiError
        ? toFriendlyErrorMessage(error.errorCode, error.message)
        : "The link could not be deleted.");
    } finally {
      setBusyCode(null);
    }
  };

  const handleBulkDelete = async () => {
    const codes = Array.from(selectedCodes);
    if (codes.length === 0) return;

    setIsBulkDeleting(true);
    setActionError(null);

    try {
      await Promise.all(codes.map((code) => deleteShortLink(code)));
      setLinks((current) => current.filter((link) => !selectedCodes.has(link.code)));
      setSelectedCodes(new Set());
      if (editingCode && selectedCodes.has(editingCode)) setEditingCode(null);
      showToast({
        title: "Selected links deleted",
        message: `${codes.length} link${codes.length === 1 ? "" : "s"} removed`,
        variant: "success"
      });
    } catch (error) {
      setActionError(error instanceof ApiError
        ? toFriendlyErrorMessage(error.errorCode, error.message)
        : "Selected links could not be deleted.");
    } finally {
      setIsBulkDeleting(false);
    }
  };

  const handleBulkStatusChange = async (nextIsActive: boolean) => {
    const codes = Array.from(selectedCodes);
    if (codes.length === 0) return;

    setIsBulkUpdating(true);
    setActionError(null);

    try {
      const responses = await Promise.all(
        codes.map((code) => nextIsActive ? activateShortLink(code) : deactivateShortLink(code))
      );
      const updatedCodes = new Set(responses.map((response) => response.code));
      setLinks((current) =>
        current.map((link) =>
          updatedCodes.has(link.code) ? { ...link, isActive: nextIsActive } : link
        )
      );
      setSelectedCodes(new Set());
      showToast({
        title: nextIsActive ? "Selected links activated" : "Selected links deactivated",
        message: `${codes.length} link${codes.length === 1 ? "" : "s"} updated`,
        variant: "success"
      });
    } catch (error) {
      setActionError(error instanceof ApiError
        ? toFriendlyErrorMessage(error.errorCode, error.message)
        : nextIsActive
          ? "Selected links could not be activated."
          : "Selected links could not be deactivated.");
    } finally {
      setIsBulkUpdating(false);
    }
  };

  return {
    actionError,
    setActionError,
    busyCode,
    isBulkDeleting,
    isBulkUpdating,
    isCreating,
    editingCode,
    editingLink,
    isEditorOpen,
    editForm,
    setEditForm,
    fieldErrors,
    setFieldErrors,
    editorRequestError,
    hasEditChanges,
    handleDeactivate,
    handleActivate,
    startEdit,
    startCreate,
    closeEditor,
    handleCreate,
    handleSaveEdit,
    handleDelete,
    handleBulkDelete,
    handleBulkStatusChange
  };
}

export function toEditorExpiryValue(value: string | null): string {
  if (!value) return "";

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "" : formatDateTimeLocal(date);
}
