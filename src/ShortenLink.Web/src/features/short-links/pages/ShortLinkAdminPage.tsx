import { useEffect, useRef, useState } from "react";
import { ApiError } from "../api/http";
import {
  activateShortLink,
  createShortLink,
  deactivateShortLink,
  deleteShortLink,
  getShortLinkAnalytics,
  listShortLinks,
  updateShortLink
} from "../api/shortLinksApi";
import { getAdminPermissionState } from "../api/adminSecurity";
import type { ShortLinkAdminItem, ShortLinkAnalytics, ShortLinkDiscoveryQuery } from "../types";
import { formatDateTime, toFriendlyErrorMessage } from "../types";
import { getExpiryPresentation } from "../expiryPresentation";
import { Badge } from "../../../shared/components/ui/badge";
import { Button } from "../../../shared/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "../../../shared/components/ui/card";
import { ConfirmDialog } from "../../../shared/components/ConfirmDialog";
import { EmptyState } from "../../../shared/components/EmptyState";
import { TableSkeleton } from "../../../shared/components/TableSkeleton";
import { showToast } from "../../../shared/toast";
import {
  createRecoveryNotice,
  shouldPreserveMutationContext,
  type RecoveryNotice
} from "../../../shared/api/recovery";
import { RowActionsMenu } from "../../../shared/components/RowActionsMenu";
import { Input } from "../../../shared/components/ui/input";
import { Label } from "../../../shared/components/ui/label";
import { DataTable } from "../../../shared/components/DataTable";
import { Pagination } from "../../../shared/components/Pagination";
import { ExpiryQuickPicks } from "../components/ExpiryQuickPicks";
import { ShortLinkShareDialog } from "../components/ShortLinkShareDialog";
import { ShortLinkQrDialog } from "../components/ShortLinkQrDialog";
import { downloadShortLinksCsv } from "../export";
import {
  defaultShortLinkDiscoveryQuery,
  createShortLinkDiscoveryChange,
  hasShortLinkDiscoveryCriteria,
  ShortLinkDiscoveryToolbar
} from "../components/ShortLinkDiscoveryToolbar";
import {
  hasShortLinkFieldErrors,
  mapShortLinkApiFieldErrors,
  validateShortLinkForm,
  type ShortLinkFieldErrors
} from "../validation";

type ShortLinkAdminPageProps = {
  onDirtyChange?: (isDirty: boolean) => void;
};

type ConfirmAction = {
  title: string;
  description: string;
  confirmLabel: string;
  variant?: "default" | "destructive";
  onConfirm: () => void;
};

export function ShortLinkAdminPage({ onDirtyChange }: ShortLinkAdminPageProps) {
  const [links, setLinks] = useState<ShortLinkAdminItem[]>([]);
  const [actionError, setActionError] = useState<string | null>(null);
  const [listFailure, setListFailure] = useState<(RecoveryNotice & { pageNumber: number }) | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [busyCode, setBusyCode] = useState<string | null>(null);
  const [isBulkDeleting, setIsBulkDeleting] = useState(false);
  const [isBulkUpdating, setIsBulkUpdating] = useState(false);
  const [isCreating, setIsCreating] = useState(false);
  const [copiedCode, setCopiedCode] = useState<string | null>(null);
  const [openMenuCode, setOpenMenuCode] = useState<string | null>(null);
  const [editingCode, setEditingCode] = useState<string | null>(null);
  const [editForm, setEditForm] = useState({ originalUrl: "", expiredAtLocal: "" });
  const [initialEditForm, setInitialEditForm] = useState({ originalUrl: "", expiredAtLocal: "" });
  const [fieldErrors, setFieldErrors] = useState<ShortLinkFieldErrors>({});
  const [editorRequestError, setEditorRequestError] = useState<string | null>(null);
  const [tooltip, setTooltip] = useState<{ text: string; x: number; y: number } | null>(null);
  const [confirmAction, setConfirmAction] = useState<ConfirmAction | null>(null);
  const [selectedCodes, setSelectedCodes] = useState<Set<string>>(() => new Set());
  const [pageSize, setPageSize] = useState(25);
  const [pageNumber, setPageNumber] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(1);
  const [discoveryQuery, setDiscoveryQuery] = useState<ShortLinkDiscoveryQuery>(
    defaultShortLinkDiscoveryQuery
  );
  const [analyticsCode, setAnalyticsCode] = useState<string | null>(null);
  const [sharingLink, setSharingLink] = useState<ShortLinkAdminItem | null>(null);
  const [qrLink, setQrLink] = useState<ShortLinkAdminItem | null>(null);
  const [analyticsData, setAnalyticsData] = useState<ShortLinkAnalytics | null>(null);
  const [analyticsError, setAnalyticsError] = useState<string | null>(null);
  const [isAnalyticsRetryable, setIsAnalyticsRetryable] = useState(false);
  const [isAnalyticsLoading, setIsAnalyticsLoading] = useState(false);
  const [isExporting, setIsExporting] = useState(false);
  const [exportFailure, setExportFailure] = useState<RecoveryNotice | null>(null);
  const copyFeedbackTimeoutRef = useRef<number | null>(null);
  const adminPermissions = getAdminPermissionState();

  const selectedLinks = links.filter((link) => selectedCodes.has(link.code));
  const selectedCount = selectedCodes.size;
  const canEditLink = (link: ShortLinkAdminItem) =>
    link.accessLevel === "Admin" || link.accessLevel === "Owner" || link.accessLevel === "Edit";
  const canManageLink = (link: ShortLinkAdminItem) =>
    link.accessLevel === "Admin" || link.accessLevel === "Owner";
  const selectedAreEditable = selectedLinks.length > 0 && selectedLinks.every(canEditLink);
  const selectedAreManaged = selectedLinks.length > 0 && selectedLinks.every(canManageLink);
  const canBulkDeactivate = adminPermissions.canDeactivate && selectedAreEditable && selectedLinks.some((link) => link.isActive);
  const canBulkActivate = adminPermissions.canActivate && selectedAreEditable && selectedLinks.some((link) => !link.isActive);
  const canBulkDelete = adminPermissions.canDelete && selectedAreManaged;
  const hasBulkActions = canBulkDeactivate || canBulkActivate || canBulkDelete;
  const shouldShowList = !isLoading && links.length > 0;
  const editingLink = editingCode
    ? links.find((link) => link.code === editingCode) ?? null
    : null;
  const isEditorOpen = isCreating || editingLink !== null;
  const hasEditChanges = isEditorOpen
    && (editForm.originalUrl !== initialEditForm.originalUrl
      || editForm.expiredAtLocal !== initialEditForm.expiredAtLocal);

  const loadLinks = async (nextPageNumber = pageNumber) => {
    setIsLoading(true);
    setListFailure(null);

    try {
      const result = await listShortLinks(pageSize, nextPageNumber, discoveryQuery);
      setLinks(result.items);
      setTotalCount(result.totalCount ?? result.items.length);
      setTotalPages(result.totalPages ?? 1);
      setSelectedCodes(new Set());
      setPageNumber(result.page ?? nextPageNumber);
    } catch (error) {
      const message = error instanceof ApiError
        ? toFriendlyErrorMessage(error.errorCode, error.message)
        : "We could not load links right now.";
      setListFailure({
        ...createRecoveryNotice(error, message),
        pageNumber: nextPageNumber
      });
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    void loadLinks(1);

    return () => {
      if (copyFeedbackTimeoutRef.current !== null) {
        window.clearTimeout(copyFeedbackTimeoutRef.current);
      }
    };
  }, [pageSize, discoveryQuery]);

  useEffect(() => {
    onDirtyChange?.(hasEditChanges);
  }, [hasEditChanges, onDirtyChange]);

  useEffect(() => {
    const handleBeforeUnload = (event: BeforeUnloadEvent) => {
      if (!hasEditChanges) {
        return;
      }

      event.preventDefault();
      event.returnValue = "";
    };

    window.addEventListener("beforeunload", handleBeforeUnload);
    return () => window.removeEventListener("beforeunload", handleBeforeUnload);
  }, [hasEditChanges]);

  const handleCopy = async (link: ShortLinkAdminItem, trigger: HTMLElement) => {
    try {
      await navigator.clipboard.writeText(link.shortUrl);
      const rect = trigger.getBoundingClientRect();
      if (copyFeedbackTimeoutRef.current !== null) {
        window.clearTimeout(copyFeedbackTimeoutRef.current);
      }

      setCopiedCode(link.code);
      setTooltip({
        text: "Copied",
        x: rect.left,
        y: rect.top
      });
      copyFeedbackTimeoutRef.current = window.setTimeout(() => {
        setCopiedCode(null);
        setTooltip(null);
        copyFeedbackTimeoutRef.current = null;
      }, 1500);
    } catch {
      setActionError("Clipboard access was blocked, so the URL could not be copied.");
    }
  };

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
      if (error instanceof ApiError) {
        setActionError(toFriendlyErrorMessage(error.errorCode, error.message));
      } else {
        setActionError("The link could not be deactivated.");
      }
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
      if (error instanceof ApiError) {
        setActionError(toFriendlyErrorMessage(error.errorCode, error.message));
      } else {
        setActionError("The link could not be activated.");
      }
    } finally {
      setBusyCode(null);
    }
  };

  const startEdit = (link: ShortLinkAdminItem) => {
    if (!adminPermissions.canUpdate) {
      return;
    }

    setIsCreating(false);
    setEditingCode(link.code);
    setOpenMenuCode(null);
    setActionError(null);
    setEditorRequestError(null);
    setFieldErrors({});
    const nextForm = {
      originalUrl: link.originalUrl,
      expiredAtLocal: toDateTimeLocalValue(link.expiredAtUtc)
    };
    setEditForm(nextForm);
    setInitialEditForm(nextForm);
  };

  const startCreate = () => {
    if (!adminPermissions.canCreate) {
      return;
    }

    const nextForm = { originalUrl: "", expiredAtLocal: "" };
    setIsCreating(true);
    setEditingCode(null);
    setOpenMenuCode(null);
    setActionError(null);
    setEditorRequestError(null);
    setFieldErrors({});
    setEditForm(nextForm);
    setInitialEditForm(nextForm);
  };

  const closeEditor = () => {
    setIsCreating(false);
    setEditingCode(null);
    setFieldErrors({});
    setInitialEditForm({ originalUrl: "", expiredAtLocal: "" });
    setEditorRequestError(null);
  };

  const validateEditorForm = () => {
    return validateShortLinkForm(editForm);
  };

  const applyApiFieldError = (error: ApiError) => {
    const nextErrors = mapShortLinkApiFieldErrors(error.fieldErrors);
    setFieldErrors(nextErrors);
    return hasShortLinkFieldErrors(nextErrors);
  };

  const handleCreate = async () => {
    const nextErrors = validateEditorForm();
    if (hasShortLinkFieldErrors(nextErrors)) {
      setFieldErrors(nextErrors);
      return;
    }
    setFieldErrors({});

    const payload = {
      originalUrl: editForm.originalUrl.trim(),
      expiredAtUtc: new Date(editForm.expiredAtLocal).toISOString()
    };

    setBusyCode("__create__");
    setActionError(null);
    setEditorRequestError(null);

    try {
      const created = await createShortLink(payload);
      closeEditor();
      await loadLinks(1);
      showToast({
        title: "Short link created",
        message: created.code,
        variant: "success"
      });
    } catch (error) {
      if (error instanceof ApiError && applyApiFieldError(error)) {
        return;
      }

      const message =
        error instanceof ApiError
          ? toFriendlyErrorMessage(error.errorCode, error.message)
          : "The link could not be created.";

      if (shouldPreserveMutationContext(error)) {
        setEditorRequestError(message);
        return;
      }

      closeEditor();
      showToast({
        title: "Create failed",
        message,
        variant: "error"
      });
    } finally {
      setBusyCode(null);
    }
  };

  const handleSaveEdit = async (code: string) => {
    const nextErrors = validateEditorForm();
    if (hasShortLinkFieldErrors(nextErrors)) {
      setFieldErrors(nextErrors);
      return;
    }
    setFieldErrors({});

    const payload = {
      originalUrl: editForm.originalUrl.trim(),
      expiredAtUtc: new Date(editForm.expiredAtLocal).toISOString()
    };

    setBusyCode(code);
    setActionError(null);
    setEditorRequestError(null);

    try {
      const updated = await updateShortLink(code, payload);
      setLinks((current) =>
        current.map((link) => (
          link.code === updated.code
            ? { ...updated, accessLevel: updated.accessLevel ?? link.accessLevel }
            : link
        ))
      );
      closeEditor();
      showToast({
        title: "Short link updated",
        message: code,
        variant: "success"
      });
    } catch (error) {
      if (error instanceof ApiError && applyApiFieldError(error)) {
        return;
      }

      const message =
        error instanceof ApiError
          ? toFriendlyErrorMessage(error.errorCode, error.message)
          : "The link could not be updated.";

      if (shouldPreserveMutationContext(error)) {
        setEditorRequestError(message);
        return;
      }

      closeEditor();
      showToast({
        title: "Update failed",
        message,
        variant: "error"
      });
    } finally {
      setBusyCode(null);
    }
  };

  const handleDelete = async (code: string) => {
    setBusyCode(code);
    setOpenMenuCode(null);
    setActionError(null);

    try {
      const response = await deleteShortLink(code);
      setLinks((current) => current.filter((link) => link.code !== response.code));
      setSelectedCodes((current) => {
        const next = new Set(current);
        next.delete(response.code);
        return next;
      });
      if (editingCode === response.code) {
        setEditingCode(null);
      }
      if (analyticsCode === response.code) {
        closeAnalyticsPanel();
      }
      showToast({
        title: "Short link deleted",
        message: response.code,
        variant: "success"
      });
    } catch (error) {
      if (error instanceof ApiError) {
        setActionError(toFriendlyErrorMessage(error.errorCode, error.message));
      } else {
        setActionError("The link could not be deleted.");
      }
    } finally {
      setBusyCode(null);
    }
  };

  const handleBulkDelete = async () => {
    const codes = Array.from(selectedCodes);
    if (codes.length === 0) {
      return;
    }

    setIsBulkDeleting(true);
    setActionError(null);

    try {
      await Promise.all(codes.map((code) => deleteShortLink(code)));
      setLinks((current) => current.filter((link) => !selectedCodes.has(link.code)));
      setSelectedCodes(new Set());
      if (editingCode && selectedCodes.has(editingCode)) {
        setEditingCode(null);
      }
      showToast({
        title: "Selected links deleted",
        message: `${codes.length} link${codes.length === 1 ? "" : "s"} removed`,
        variant: "success"
      });
    } catch (error) {
      if (error instanceof ApiError) {
        setActionError(toFriendlyErrorMessage(error.errorCode, error.message));
      } else {
        setActionError("Selected links could not be deleted.");
      }
    } finally {
      setIsBulkDeleting(false);
    }
  };

  const handleBulkStatusChange = async (nextIsActive: boolean) => {
    const codes = Array.from(selectedCodes);
    if (codes.length === 0) {
      return;
    }

    setIsBulkUpdating(true);
    setActionError(null);

    try {
      const responses = await Promise.all(
        codes.map((code) =>
          nextIsActive ? activateShortLink(code) : deactivateShortLink(code)
        )
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
      if (error instanceof ApiError) {
        setActionError(toFriendlyErrorMessage(error.errorCode, error.message));
      } else {
        setActionError(nextIsActive
          ? "Selected links could not be activated."
          : "Selected links could not be deactivated.");
      }
    } finally {
      setIsBulkUpdating(false);
    }
  };

  const requestDelete = (code: string) => {
    if (!adminPermissions.canDelete) {
      return;
    }

    setOpenMenuCode(null);
    setConfirmAction({
      title: "Delete short link?",
      description: `This will permanently delete ${code}. This action cannot be undone.`,
      confirmLabel: "Delete",
      variant: "destructive",
      onConfirm: () => void handleDelete(code)
    });
  };

  const requestStatusChange = (link: ShortLinkAdminItem) => {
    if ((link.isActive && !adminPermissions.canDeactivate)
      || (!link.isActive && !adminPermissions.canActivate)) {
      return;
    }

    setOpenMenuCode(null);
    setConfirmAction({
      title: link.isActive ? "Deactivate short link?" : "Activate short link?",
      description: link.isActive
        ? `Deactivate ${link.code}? Redirects for this link will stop working.`
        : `Activate ${link.code}? Redirects for this link will start working again.`,
      confirmLabel: link.isActive ? "Deactivate" : "Activate",
      variant: link.isActive ? "destructive" : "default",
      onConfirm: () => {
        if (link.isActive) {
          void handleDeactivate(link.code);
        } else {
          void handleActivate(link.code);
        }
      }
    });
  };

  const loadAnalytics = async (code: string) => {
    setAnalyticsData(null);
    setAnalyticsError(null);
    setIsAnalyticsRetryable(false);
    setIsAnalyticsLoading(true);

    try {
      const analytics = await getShortLinkAnalytics(code);
      setAnalyticsData(analytics);
    } catch (error) {
      if (error instanceof ApiError) {
        setAnalyticsError(toFriendlyErrorMessage(error.errorCode, error.message));
        setIsAnalyticsRetryable(error.retryable);
      } else {
        setAnalyticsError("Analytics could not be loaded.");
      }
    } finally {
      setIsAnalyticsLoading(false);
    }
  };

  const openAnalyticsPanel = (link: ShortLinkAdminItem) => {
    if (!adminPermissions.canReadAnalytics) {
      return;
    }

    setAnalyticsCode(link.code);
    setOpenMenuCode(null);
    void loadAnalytics(link.code);
  };

  const closeAnalyticsPanel = () => {
    setAnalyticsCode(null);
    setAnalyticsData(null);
    setAnalyticsError(null);
    setIsAnalyticsRetryable(false);
    setIsAnalyticsLoading(false);
  };

  const requestBulkDelete = () => {
    if (!adminPermissions.canDelete) {
      return;
    }

    setConfirmAction({
      title: "Delete selected links?",
      description: `This will permanently delete ${selectedCount} selected link${selectedCount === 1 ? "" : "s"}. This action cannot be undone.`,
      confirmLabel: "Delete selected",
      variant: "destructive",
      onConfirm: () => void handleBulkDelete()
    });
  };

  const requestBulkStatusChange = (nextIsActive: boolean) => {
    if ((nextIsActive && !adminPermissions.canActivate)
      || (!nextIsActive && !adminPermissions.canDeactivate)) {
      return;
    }

    setConfirmAction({
      title: nextIsActive ? "Activate selected links?" : "Deactivate selected links?",
      description: nextIsActive
        ? `Activate ${selectedCount} selected link${selectedCount === 1 ? "" : "s"}?`
        : `Deactivate ${selectedCount} selected link${selectedCount === 1 ? "" : "s"}? Redirects for these links will stop working.`,
      confirmLabel: nextIsActive ? "Activate selected" : "Deactivate selected",
      variant: nextIsActive ? "default" : "destructive",
      onConfirm: () => void handleBulkStatusChange(nextIsActive)
    });
  };

  const goToPage = (nextPageNumber: number) => {
    void loadLinks(Math.max(1, Math.min(nextPageNumber, totalPages)));
  };

  const handleDiscoveryChange = (nextQuery: ShortLinkDiscoveryQuery) => {
    const change = createShortLinkDiscoveryChange(nextQuery);
    setPageNumber(change.pageNumber);
    setDiscoveryQuery(change.query);
  };

  const handleExport = async () => {
    if (isExporting) {
      return;
    }

    setIsExporting(true);
    setExportFailure(null);

    try {
      const firstPage = await listShortLinks(200, 1, discoveryQuery);
      const allLinks = [...firstPage.items];
      const totalPages = Math.max(firstPage.totalPages ?? 1, 1);

      for (let page = 2; page <= totalPages; page += 1) {
        const nextPage = await listShortLinks(200, page, discoveryQuery);
        const knownCodes = new Set(allLinks.map((link) => link.code));
        allLinks.push(...nextPage.items.filter((link) => !knownCodes.has(link.code)));
      }

      downloadShortLinksCsv(allLinks);
      showToast({
        title: "Short links exported",
        message: `${allLinks.length} link${allLinks.length === 1 ? "" : "s"} downloaded`,
        variant: "success"
      });
    } catch (error) {
      const message = error instanceof ApiError
        ? toFriendlyErrorMessage(error.errorCode, error.message)
        : "The short-link export could not be created.";
      setExportFailure(createRecoveryNotice(error, message));
    } finally {
      setIsExporting(false);
    }
  };

  const hasRowActions = (link: ShortLinkAdminItem) =>
    Boolean(link.shortUrl)
    || adminPermissions.canReadAnalytics
    || (canEditLink(link) && adminPermissions.canUpdate)
    || (canEditLink(link) && (link.isActive ? adminPermissions.canDeactivate : adminPermissions.canActivate))
    || (canManageLink(link) && adminPermissions.canDelete);

  const renderDestination = (link: ShortLinkAdminItem) => (
    <a
      className="destination-link"
      href={link.originalUrl}
      target="_blank"
      rel="noreferrer"
      onBlur={() => setTooltip(null)}
      onFocus={(event) => {
        const rect = event.currentTarget.getBoundingClientRect();
        setTooltip({ text: link.originalUrl, x: rect.left, y: rect.top });
      }}
      onMouseEnter={(event) => setTooltip({ text: link.originalUrl, x: event.clientX, y: event.clientY })}
      onMouseLeave={() => setTooltip(null)}
      onMouseMove={(event) => setTooltip({ text: link.originalUrl, x: event.clientX, y: event.clientY })}
    >
      {link.originalUrl}
    </a>
  );

  const renderActions = (link: ShortLinkAdminItem) => (
    <div className="admin-row-actions">
      <button
        className={copiedCode === link.code ? "copy-icon-button copy-icon-button-done" : "copy-icon-button"}
        type="button"
        disabled={copiedCode === link.code}
        aria-label={`Copy short URL for ${link.code}`}
        title={copiedCode === link.code ? "Copied" : "Copy"}
        onClick={(event) => handleCopy(link, event.currentTarget)}
      >
        <span aria-hidden="true" />
      </button>
      {hasRowActions(link) ? (
        <RowActionsMenu
          label={`Actions for ${link.code}`}
          open={openMenuCode === link.code}
          onOpenChange={(open) => setOpenMenuCode(open ? link.code : null)}
          actions={[
            ...(link.shortUrl ? [{ id: "qr", label: "QR code", onSelect: () => setQrLink(link) }] : []),
            ...(adminPermissions.canReadAnalytics ? [{ id: "analytics", label: "Analytics", onSelect: () => void openAnalyticsPanel(link) }] : []),
            ...(canEditLink(link) && adminPermissions.canUpdate ? [{ id: "edit", label: "Edit", onSelect: () => startEdit(link) }] : []),
            ...(canManageLink(link) ? [{ id: "share", label: "Share", onSelect: () => setSharingLink(link) }] : []),
            ...(canEditLink(link) && (link.isActive ? adminPermissions.canDeactivate : adminPermissions.canActivate) ? [{
              id: "status",
              label: busyCode === link.code ? "Updating" : link.isActive ? "Deactivate" : "Activate",
              disabled: busyCode === link.code,
              onSelect: () => requestStatusChange(link)
            }] : []),
            ...(canManageLink(link) && adminPermissions.canDelete ? [{
              id: "delete",
              label: "Delete",
              destructive: true,
              disabled: busyCode === link.code,
              onSelect: () => requestDelete(link.code)
            }] : [])
          ]}
        />
      ) : null}
    </div>
  );

  return (
    <>
      <nav className="page-breadcrumb-bar" aria-label="Breadcrumb">
        <ol className="page-breadcrumb">
          <li>Shorten Link</li>
          <li aria-current="page">Short links management</li>
        </ol>
      </nav>
      <Card className="admin-panel">
        <CardHeader className="panel-heading-wide">
          <div>
            <p className="eyebrow">Short links</p>
            <CardTitle>Manage generated short links</CardTitle>
          </div>
          <Button
            disabled={!adminPermissions.canCreate}
            title={adminPermissions.canCreate ? "Create" : "Missing short_links.create permission"}
            onClick={startCreate}
          >
            Create
          </Button>
        </CardHeader>
        <CardContent>
      <ShortLinkDiscoveryToolbar
        value={discoveryQuery}
        disabled={isLoading}
        onChange={handleDiscoveryChange}
        action={
          <Button
            type="button"
            variant="secondary"
            disabled={isLoading || isExporting}
            onClick={() => void handleExport()}
          >
            {isExporting ? "Exporting..." : "Export CSV"}
          </Button>
        }
      />

      {exportFailure ? (
        <div className="recovery-banner recovery-banner-error" role="alert">
          <span>{exportFailure.message}</span>
          {exportFailure.retryable ? (
            <Button variant="secondary" onClick={() => void handleExport()}>Retry export</Button>
          ) : (
            <Button variant="ghost" onClick={() => setExportFailure(null)}>Dismiss</Button>
          )}
        </div>
      ) : null}

      {isLoading ? <TableSkeleton /> : null}

      {!isLoading && listFailure && links.length === 0 ? (
        <EmptyState
          title="Links could not be loaded"
          description={listFailure.message}
          action={listFailure.retryable
            ? <Button variant="secondary" onClick={() => void loadLinks(listFailure.pageNumber)}>Retry</Button>
            : undefined}
        />
      ) : null}

      {!isLoading && listFailure && links.length > 0 ? (
        <div className="recovery-banner" role="alert">
          <span>{listFailure.message}</span>
          {listFailure.retryable ? (
            <Button variant="secondary" onClick={() => void loadLinks(listFailure.pageNumber)}>
              Retry
            </Button>
          ) : null}
        </div>
      ) : null}

      {actionError ? (
        <div className="recovery-banner recovery-banner-error" role="alert">
          <span>{actionError}</span>
          <Button variant="ghost" onClick={() => setActionError(null)}>Dismiss</Button>
        </div>
      ) : null}

      {!isLoading && !listFailure && !shouldShowList ? (
        <EmptyState
          title={hasShortLinkDiscoveryCriteria(discoveryQuery) ? "No matching links" : "No data"}
          description={hasShortLinkDiscoveryCriteria(discoveryQuery)
            ? "Try a different search, status, or sort selection."
            : "Create a short link first, then manage it here."}
          action={hasShortLinkDiscoveryCriteria(discoveryQuery)
            ? <Button variant="secondary" onClick={() => handleDiscoveryChange(defaultShortLinkDiscoveryQuery)}>Clear filters</Button>
            : undefined}
        />
      ) : null}

      {shouldShowList ? (
        <DataTable
          ariaLabel="Short links"
          rows={links}
          getRowKey={(link) => link.code}
          bulkSelection={hasBulkActions ? {
            selectedKeys: selectedCodes,
            onChange: setSelectedCodes,
            getRowLabel: (link) => `Select ${link.code}`,
            clearDisabled: isBulkDeleting || isBulkUpdating,
            actions: [
              ...(canBulkDeactivate ? [{
                id: "deactivate",
                label: isBulkUpdating ? "Updating..." : (count: number) => `Deactivate selected (${count})`,
                disabled: isBulkUpdating || isBulkDeleting,
                onSelect: () => requestBulkStatusChange(false)
              }] : []),
              ...(canBulkActivate ? [{
                id: "activate",
                label: isBulkUpdating ? "Updating..." : (count: number) => `Activate selected (${count})`,
                disabled: isBulkUpdating || isBulkDeleting,
                onSelect: () => requestBulkStatusChange(true)
              }] : []),
              ...(canBulkDelete ? [{
                id: "delete",
                label: isBulkDeleting ? "Deleting..." : (count: number) => `Delete selected (${count})`,
                variant: "destructive" as const,
                disabled: isBulkDeleting || isBulkUpdating,
                onSelect: requestBulkDelete
              }] : [])
            ]
          } : undefined}
          columns={[
            { id: "code", header: "Code", cell: (link) => <a href={link.shortUrl} target="_blank" rel="noreferrer">{link.code}</a> },
            { id: "destination", header: "Destination", cellProps: { className: "admin-url-cell" }, cell: renderDestination },
            {
              id: "createdBy",
              header: "Created by",
              cell: (link) => (
                <div className="creator-cell">
                  <span>{link.createdByDisplayName ?? link.createdByUsername ?? "Unknown"}</span>
                  {link.createdByUsername && link.createdByDisplayName ? <small>@{link.createdByUsername}</small> : null}
                </div>
              )
            },
            { id: "access", header: "Access", cell: (link) => <Badge variant="secondary">{link.accessLevel ?? "Unknown"}</Badge> },
            { id: "created", header: "Created", cell: (link) => formatDateTime(link.createdAtUtc) },
            {
              id: "expiry",
              header: "Expiry (local time)",
              cell: (link) => {
                const expiry = getExpiryPresentation(link, new Date());
                return (
                  <div className="expiry-cell">
                    <time dateTime={link.expiredAtUtc ?? undefined}>{expiry.dateTime}</time>
                    {expiry.state !== "active" && expiry.state !== "unknown" ? (
                      <Badge
                        variant={expiry.state === "expiring-soon" ? "secondary" : "destructive"}
                        className={`expiry-badge expiry-badge-${expiry.state}`}
                      >
                        {expiry.label}
                      </Badge>
                    ) : null}
                    {expiry.state === "expiring-soon" ? <small>{expiry.detail}</small> : null}
                  </div>
                );
              }
            },
            { id: "status", header: "Status", cell: (link) => <Badge variant={link.isActive ? "default" : "destructive"}>{link.isActive ? "Active" : "Inactive"}</Badge> },
            { id: "actions", header: "Actions", cell: renderActions }
          ]}
        />
      ) : null}
      {shouldShowList ? (
        <Pagination
          totalItems={totalCount}
          page={pageNumber}
          totalPages={totalPages}
          pageSize={pageSize}
          onPageChange={goToPage}
          onPageSizeChange={setPageSize}
        />
      ) : null}
      {tooltip ? (
        <div
          className="floating-tooltip"
          style={{
            left: tooltip.x + 12,
            top: tooltip.y - 12
          }}
        >
          {tooltip.text}
        </div>
      ) : null}
      {isEditorOpen ? (
        <div className="dialog-backdrop" role="presentation">
          <div
            className="edit-dialog"
            role="dialog"
            aria-modal="true"
            aria-labelledby="edit-dialog-title"
          >
            <div>
              <p className="eyebrow">{isCreating ? "Create" : "Edit"}</p>
              <h2 id="edit-dialog-title">
                {isCreating ? "Create short link" : `Update ${editingLink?.code}`}
              </h2>
            </div>
            <Label className="field">
              <span className="field-label">
                Destination URL <span className="required-marker">*</span>
              </span>
              <Input
                type="url"
                required
                aria-invalid={fieldErrors.originalUrl ? "true" : undefined}
                aria-describedby={fieldErrors.originalUrl ? "editor-original-url-error" : undefined}
                value={editForm.originalUrl}
                onChange={(event) => {
                  const { value } = event.target;
                  setEditForm((current) => ({
                    ...current,
                    originalUrl: value
                  }));
                  setFieldErrors((current) => ({
                    ...current,
                    originalUrl: undefined
                  }));
                }}
              />
              {fieldErrors.originalUrl ? (
                <span id="editor-original-url-error" className="field-error">
                  {fieldErrors.originalUrl}
                </span>
              ) : null}
            </Label>
            <Label className="field">
              <span className="field-label">
                Expiry <span className="required-marker">*</span>
              </span>
              <Input
                type="datetime-local"
                required
                aria-invalid={fieldErrors.expiredAtLocal ? "true" : undefined}
                aria-describedby={fieldErrors.expiredAtLocal ? "editor-expiry-error" : undefined}
                value={editForm.expiredAtLocal}
                onChange={(event) => {
                  const { value } = event.target;
                  setEditForm((current) => ({
                    ...current,
                    expiredAtLocal: value
                  }));
                  setFieldErrors((current) => ({
                    ...current,
                    expiredAtLocal: undefined
                  }));
                }}
              />
              {fieldErrors.expiredAtLocal ? (
                <span id="editor-expiry-error" className="field-error">
                  {fieldErrors.expiredAtLocal}
                </span>
              ) : null}
              <ExpiryQuickPicks
                onChange={(expiredAtLocal) => {
                  setEditForm((current) => ({
                    ...current,
                    expiredAtLocal
                  }));
                  setFieldErrors((current) => ({
                    ...current,
                    expiredAtLocal: undefined
                  }));
                }}
              />
            </Label>
            {editorRequestError ? (
              <div className="recovery-banner recovery-banner-error" role="alert">
                <span>{editorRequestError} Your changes are still here; choose Save to try again.</span>
              </div>
            ) : null}
            <div className="dialog-actions">
              <Button
                variant="secondary"
                onClick={closeEditor}
              >
                Cancel
              </Button>
              <Button
                disabled={busyCode === (isCreating ? "__create__" : editingLink?.code)}
                onClick={() => {
                  if (isCreating) {
                    void handleCreate();
                  } else if (editingLink) {
                    void handleSaveEdit(editingLink.code);
                  }
                }}
              >
                {busyCode === (isCreating ? "__create__" : editingLink?.code)
                  ? "Saving"
                  : isCreating
                    ? "Create"
                    : "Save changes"}
              </Button>
            </div>
          </div>
        </div>
      ) : null}
      <ShortLinkShareDialog link={sharingLink} onClose={() => setSharingLink(null)} />
      <ShortLinkQrDialog link={qrLink} onClose={() => setQrLink(null)} />
      {analyticsCode ? (
        <div className="dialog-backdrop" role="presentation">
          <div
            className="analytics-dialog"
            role="dialog"
            aria-modal="true"
            aria-labelledby="analytics-dialog-title"
          >
            <div className="analytics-dialog-header">
              <div>
                <p className="eyebrow">Analytics</p>
                <h2 id="analytics-dialog-title">{analyticsCode}</h2>
              </div>
              <Button variant="secondary" onClick={closeAnalyticsPanel}>
                Close
              </Button>
            </div>

            {isAnalyticsLoading ? (
              <div className="analytics-loading">
                <span className="skeleton skeleton-button" />
                <span className="skeleton skeleton-url" />
                <span className="skeleton skeleton-url" />
              </div>
            ) : null}

            {!isAnalyticsLoading && analyticsError ? (
              <EmptyState
                title="Analytics unavailable"
                description={analyticsError}
                action={isAnalyticsRetryable
                  ? <Button variant="secondary" onClick={() => void loadAnalytics(analyticsCode)}>Retry</Button>
                  : undefined}
              />
            ) : null}

            {!isAnalyticsLoading && !analyticsError && analyticsData ? (
              <div className="analytics-panel">
                <div className="analytics-metrics">
                  <div>
                    <span>Clicks</span>
                    <strong>{analyticsData.clickCount}</strong>
                  </div>
                  <div>
                    <span>Last clicked</span>
                    <strong>{analyticsData.lastClickedAtUtc ? formatDateTime(analyticsData.lastClickedAtUtc) : "No clicks yet"}</strong>
                  </div>
                </div>

                {analyticsData.recentClicks.length === 0 ? (
                  <EmptyState
                    title="No clicks yet"
                    description="Redirect analytics will appear here after visitors use this short link."
                  />
                ) : (
                  <div className="analytics-activity-list">
                    {analyticsData.recentClicks.map((click, index) => (
                      <div className="analytics-activity-item" key={`${click.clickedAtUtc}-${index}`}>
                        <div>
                          <span className="activity-time">{formatDateTime(click.clickedAtUtc)}</span>
                          <strong>{click.userAgent || "Unknown user agent"}</strong>
                        </div>
                        <dl>
                          <div>
                            <dt>Referrer</dt>
                            <dd>{click.referrer || "Direct or unavailable"}</dd>
                          </div>
                          <div>
                            <dt>Remote IP</dt>
                            <dd>{click.remoteIpAddress || "Unavailable"}</dd>
                          </div>
                        </dl>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            ) : null}
          </div>
        </div>
      ) : null}
        </CardContent>
        <ConfirmDialog
        open={confirmAction !== null}
        title={confirmAction?.title ?? ""}
        description={confirmAction?.description ?? ""}
        confirmLabel={confirmAction?.confirmLabel ?? "Confirm"}
        variant={confirmAction?.variant}
        onConfirm={() => {
          const action = confirmAction;
          setConfirmAction(null);
          action?.onConfirm();
        }}
        onCancel={() => setConfirmAction(null)}
        />
      </Card>
    </>
  );
}

function toDateTimeLocalValue(value: string | null): string {
  if (!value) {
    return "";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "";
  }

  const offsetMs = date.getTimezoneOffset() * 60_000;
  return new Date(date.getTime() - offsetMs).toISOString().slice(0, 16);
}
