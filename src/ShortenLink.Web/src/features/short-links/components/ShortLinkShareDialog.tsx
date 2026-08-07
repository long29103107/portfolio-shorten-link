import { useEffect, useState } from "react";
import {
  deleteShortLinkShare,
  listShortLinkShares,
  setShortLinkSharingMode,
  upsertShortLinkShare
} from "../api/shortLinksApi";
import { ApiError } from "../api/http";
import { HTTP_STATUS } from "../../../shared/constants/http";
import type { ShortLinkAdminItem, ShortLinkShare, ShortLinkSharingMode } from "../types";
import { ConfirmDialog } from "../../../shared/components/ConfirmDialog";
import { Button } from "../../../shared/components/ui/button";
import { Input } from "../../../shared/components/ui/input";
import { Label } from "../../../shared/components/ui/label";

type ShortLinkShareDialogProps = {
  link: ShortLinkAdminItem | null;
  onClose: () => void;
};

export function ShortLinkShareDialog({ link, onClose }: ShortLinkShareDialogProps) {
  const [shares, setShares] = useState<ShortLinkShare[]>([]);
  const [emailInput, setEmailInput] = useState("");
  const [access, setAccess] = useState<"View" | "Edit">("View");
  const [sharingMode, setSharingMode] = useState<ShortLinkSharingMode>("AllowList");
  const [isLoading, setIsLoading] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [pendingRemoval, setPendingRemoval] = useState<ShortLinkShare | null>(null);

  useEffect(() => {
    if (!link) return;
    setIsLoading(true);
    setError(null);
    setSharingMode("AllowList");
    setShares([]);
    void listShortLinkShares(link.code)
      .then((result) => {
        setSharingMode(result.mode);
        setShares(result.items);
      })
      .catch((caught) => setError(getShareError(caught, "Sharing information could not be loaded.")))
      .finally(() => setIsLoading(false));
  }, [link]);

  if (!link) return null;

  const updateSharingMode = async (nextMode: ShortLinkSharingMode) => {
    if (nextMode === sharingMode) return;
    setIsSaving(true);
    setError(null);
    try {
      const savedMode = await setShortLinkSharingMode(link.code, nextMode);
      setSharingMode(savedMode);
    } catch (caught) {
      setError(getShareError(caught, "The sharing mode could not be updated."));
    } finally {
      setIsSaving(false);
    }
  };

  const saveShare = async () => {
    const emails = Array.from(new Set(
      emailInput
        .split(/[\s,;]+/)
        .map((email) => email.trim())
        .filter(Boolean)
    ));
    if (emails.length === 0) {
      setError("Enter one or more workspace emails to share with.");
      return;
    }
    setIsSaving(true);
    setError(null);
    try {
      const results = await Promise.allSettled(
        emails.map((email) => upsertShortLinkShare(link.code, email, access))
      );
      const saved = results.flatMap((result) => result.status === "fulfilled" ? [result.value] : []);
      const failedCount = results.length - saved.length;
      setShares((current) => {
        const savedIds = new Set(saved.map((share) => share.userId));
        return [...current.filter((share) => !savedIds.has(share.userId)), ...saved];
      });
      setEmailInput("");
      setAccess("View");
      if (failedCount > 0) {
        setError(`${failedCount} email${failedCount === 1 ? "" : "s"} could not be added. Check that they belong to this workspace.`);
      }
    } catch {
      setError("The emails could not be added. Check the addresses and try again.");
    } finally {
      setIsSaving(false);
    }
  };

  const removeShare = async () => {
    if (!pendingRemoval) return;
    setIsSaving(true);
    setError(null);
    try {
      await deleteShortLinkShare(link.code, pendingRemoval.userId);
      setShares((current) => current.filter((share) => share.userId !== pendingRemoval.userId));
      setPendingRemoval(null);
    } catch (caught) {
      setError(getShareError(caught, "Shared access could not be removed."));
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <>
      <div className="dialog-backdrop" role="presentation">
        <div className="edit-dialog share-dialog" role="dialog" aria-modal="true" aria-labelledby="share-dialog-title">
          <div className="share-dialog-header">
            <div>
              <p className="eyebrow">Share access</p>
              <div className="share-dialog-title-row">
                <h2 id="share-dialog-title">Share <code>{link.code}</code></h2>
                {!isLoading ? <span className={`share-state-badge ${sharingMode === "Public" ? "share-state-badge-public" : ""}`}>
                  {sharingMode === "Public" ? "Public" : "Allow list"}
                </span> : null}
              </div>
              <p className="share-dialog-description">Choose who can open this link inside your workspace.</p>
            </div>
            <Button className="share-dialog-close" variant="ghost" aria-label="Close sharing dialog" onClick={onClose}>
              <span className="share-dialog-close-icon" aria-hidden="true" />
            </Button>
          </div>

          <div className="share-mode-card">
            <div className="share-section-heading">
              <div>
                <strong>Who can access this link?</strong>
                <span>Public access is workspace-wide. Allow list access is invite-only.</span>
              </div>
            </div>
            <div className="share-mode-options" role="radiogroup" aria-label="Sharing mode">
              <button
                className={`share-mode-option ${sharingMode === "Public" ? "share-mode-option-active" : ""}`}
                type="button"
                role="radio"
                aria-checked={sharingMode === "Public"}
                disabled={isSaving || isLoading}
                onClick={() => void updateSharingMode("Public")}
              >
                <span className="share-mode-option-mark" aria-hidden="true">*</span>
                <span><strong>Public</strong><small>Everyone in this workspace can view it.</small></span>
              </button>
              <button
                className={`share-mode-option ${sharingMode === "AllowList" ? "share-mode-option-active" : ""}`}
                type="button"
                role="radio"
                aria-checked={sharingMode === "AllowList"}
                disabled={isSaving || isLoading}
                onClick={() => void updateSharingMode("AllowList")}
              >
                <span className="share-mode-option-mark" aria-hidden="true">@</span>
                <span><strong>Only specific emails</strong><small>Only people listed below can view it.</small></span>
              </button>
            </div>
          </div>

          {sharingMode === "AllowList" ? <>
          <div className="share-dialog-summary">
            <span className="share-dialog-summary-mark" aria-hidden="true" />
            <div>
              <strong>Permission levels</strong>
              <span><b>View</b> reads analytics - <b>Edit</b> can also update link settings</span>
            </div>
          </div>

          <div className="share-form-card">
            <div className="share-section-heading">
              <div>
                <strong>Add people by email</strong>
                <span>Paste multiple workspace emails separated by commas, spaces or new lines.</span>
              </div>
            </div>
            <div className="share-form">
              <Label className="field">
                <span className="field-label">Workspace emails</span>
                <Input
                  placeholder="alex@example.com, sam@example.com"
                  value={emailInput}
                  onChange={(event) => setEmailInput(event.target.value)}
                />
              </Label>
              <Label className="field share-access-field">
                <span className="field-label">Access</span>
                <select className="share-select" value={access} onChange={(event) => setAccess(event.target.value as "View" | "Edit")}>
                  <option value="View">View</option>
                  <option value="Edit">Edit</option>
                </select>
              </Label>
              <Button className="share-submit" disabled={isSaving} onClick={() => void saveShare()}>
                {isSaving ? "Saving..." : "Add people"}
              </Button>
            </div>
          </div>

          <div className="share-list-section">
            <div className="share-section-heading share-list-heading">
              <div>
                <strong>People with access</strong>
                <span>{isLoading ? "Loading shared access..." : "Only people listed here can open this link."}</span>
              </div>
              {!isLoading ? <span className="share-count">{shares.length}</span> : null}
            </div>
            <div className="share-list">
              {isLoading ? <div className="share-empty-state"><span className="share-empty-mark share-empty-mark-loading" aria-hidden="true" /><span>Loading shared access</span></div> : null}
              {!isLoading && shares.length === 0 ? <div className="share-empty-state"><span className="share-empty-mark" aria-hidden="true" /><div><strong>This link is private</strong><span>Add a person above to grant access.</span></div></div> : null}
              {shares.map((share) => (
                <div className="share-list-item" key={share.userId}>
                  <div>
                    <strong>{share.displayName ?? share.username ?? share.userId}</strong>
                    {share.username ? <small>@{share.username}</small> : null}
                  </div>
                  <span className="share-access-badge">{share.access}</span>
                  <Button variant="ghost" disabled={isSaving} onClick={() => setPendingRemoval(share)}>
                    Remove
                  </Button>
                </div>
              ))}
            </div>
          </div>
          </> : <div className="share-public-state">
            <span className="share-public-state-mark" aria-hidden="true">*</span>
            <div>
              <strong>Anyone in this workspace can view this link</strong>
              <span>Switch to specific emails any time to make access invite-only.</span>
            </div>
          </div>}

          {error ? <p className="field-error share-error" role="alert">{error}</p> : null}

          <div className="dialog-actions">
            <Button variant="secondary" disabled={isSaving} onClick={onClose}>Done</Button>
          </div>
        </div>
      </div>
      <ConfirmDialog
        open={pendingRemoval !== null}
        title="Remove shared access?"
        description={`Remove access for ${pendingRemoval?.displayName ?? pendingRemoval?.username ?? "this user"}?`}
        confirmLabel="Remove access"
        cancelLabel="Cancel"
        variant="destructive"
        onConfirm={() => void removeShare()}
        onCancel={() => setPendingRemoval(null)}
      />
    </>
  );
}

function getShareError(error: unknown, fallback: string): string {
  if (!(error instanceof ApiError)) {
    return fallback;
  }

  if (error.status === HTTP_STATUS.UNAUTHORIZED) {
    return "Your session expired. Sign in again and retry.";
  }

  if (error.status === HTTP_STATUS.FORBIDDEN) {
    return "Your account does not have permission to manage this link.";
  }

  return error.message || fallback;
}
