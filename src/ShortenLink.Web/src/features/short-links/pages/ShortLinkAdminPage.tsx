import { useEffect, useRef, useState } from "react";
import { getAdminPermissionState } from "../api/adminSecurity";
import type { ShortLinkAdminItem, ShortLinkDiscoveryQuery } from "../types";
import { formatDateTime } from "../types";
import { getExpiryPresentation } from "../domain/expiryPresentation";
import { Badge } from "../../../shared/components/ui/badge";
import { Button } from "../../../shared/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "../../../shared/components/ui/card";
import { ConfirmDialog } from "../../../shared/components/ConfirmDialog";
import { EmptyState } from "../../../shared/components/EmptyState";
import { TableSkeleton } from "../../../shared/components/TableSkeleton";
import { showToast } from "../../../shared/toast";
import { RowActionsMenu } from "../../../shared/components/RowActionsMenu";
import { Input } from "../../../shared/components/ui/input";
import { Label } from "../../../shared/components/ui/label";
import { DataTable } from "../../../shared/components/DataTable";
import { Pagination } from "../../../shared/components/Pagination";
import { ExpiryQuickPicks } from "../components/ExpiryQuickPicks";
import { ShortLinkShareDialog } from "../components/ShortLinkShareDialog";
import { ShortLinkQrDialog } from "../components/ShortLinkQrDialog";
import {
  defaultShortLinkDiscoveryQuery,
  createShortLinkDiscoveryChange,
  hasShortLinkDiscoveryCriteria,
  ShortLinkDiscoveryToolbar
} from "../components/ShortLinkDiscoveryToolbar";
import { useShortLinkDiscovery } from "../hooks/useShortLinkDiscovery";
import { useShortLinkAnalyticsData } from "../hooks/useShortLinkAnalyticsData";
import { useShortLinkExport } from "../hooks/useShortLinkExport";
import { useShortLinkMutations } from "../hooks/useShortLinkMutations";
import { ShortLinkAnalyticsDialog } from "../components/ShortLinkAnalyticsDialog";

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
  const [copiedCode, setCopiedCode] = useState<string | null>(null);
  const [openMenuCode, setOpenMenuCode] = useState<string | null>(null);
  const [tooltip, setTooltip] = useState<{ text: string; x: number; y: number } | null>(null);
  const [confirmAction, setConfirmAction] = useState<ConfirmAction | null>(null);
  const [selectedCodes, setSelectedCodes] = useState<Set<string>>(() => new Set());
  const [sharingLink, setSharingLink] = useState<ShortLinkAdminItem | null>(null);
  const [qrLink, setQrLink] = useState<ShortLinkAdminItem | null>(null);
  const copyFeedbackTimeoutRef = useRef<number | null>(null);
  const adminPermissions = getAdminPermissionState();
  const {
    links,
    setLinks,
    isLoading,
    listFailure,
    loadLinks,
    pageSize,
    setPageSize,
    pageNumber,
    setPageNumber,
    totalCount,
    totalPages,
    discoveryQuery,
    setDiscoveryQuery
  } = useShortLinkDiscovery();
  const {
    isExporting,
    exportFailure,
    handleExport,
    cancelExport,
    clearExportFailure
  } = useShortLinkExport(discoveryQuery);
  const {
    analyticsCode,
    analyticsData,
    analyticsError,
    isAnalyticsRetryable,
    isAnalyticsLoading,
    openAnalytics,
    closeAnalytics,
    retryAnalytics
  } = useShortLinkAnalyticsData();
  const {
    actionError,
    setActionError,
    busyCode,
    isBulkDeleting,
    isBulkUpdating,
    isCreating,
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
  } = useShortLinkMutations({
    adminPermissions,
    links,
    setLinks,
    loadLinks,
    selectedCodes,
    setSelectedCodes,
    onCloseMenu: () => setOpenMenuCode(null),
    analyticsCode,
    onAnalyticsClose: closeAnalytics
  });

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
  useEffect(() => () => {
    if (copyFeedbackTimeoutRef.current !== null) {
      window.clearTimeout(copyFeedbackTimeoutRef.current);
    }
  }, []);

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

  const openAnalyticsPanel = (link: ShortLinkAdminItem) => {
    if (!adminPermissions.canReadAnalytics) {
      return;
    }

    openAnalytics(link.code);
    setOpenMenuCode(null);
  };

  const closeAnalyticsPanel = () => {
    closeAnalytics();
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
    if (isExporting) {
      cancelExport();
    }
    const change = createShortLinkDiscoveryChange(nextQuery);
    setPageNumber(change.pageNumber);
    setDiscoveryQuery(change.query);
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
            <Button variant="ghost" onClick={clearExportFailure}>Dismiss</Button>
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
        <ShortLinkAnalyticsDialog
          code={analyticsCode}
          data={analyticsData}
          error={analyticsError}
          isRetryable={isAnalyticsRetryable}
          isLoading={isAnalyticsLoading}
          onClose={closeAnalyticsPanel}
          onRetry={retryAnalytics}
        />
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
