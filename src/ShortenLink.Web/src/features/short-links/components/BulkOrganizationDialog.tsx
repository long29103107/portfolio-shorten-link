import { useEffect, useState } from "react";
import { parseTagInput } from "../domain/organization";
import { Button } from "@/shared/components/ui/button";
import { Input } from "@/shared/components/ui/input";
import { Label } from "@/shared/components/ui/label";

type BulkOrganizationDialogProps = {
  open: boolean;
  selectedCount: number;
  isSubmitting: boolean;
  onConfirm: (folder: string, tags: string[]) => void;
  onCancel: () => void;
};

export function BulkOrganizationDialog({
  open,
  selectedCount,
  isSubmitting,
  onConfirm,
  onCancel
}: BulkOrganizationDialogProps) {
  const [folder, setFolder] = useState("");
  const [tags, setTags] = useState("");

  useEffect(() => {
    if (open) {
      setFolder("");
      setTags("");
    }
  }, [open]);

  if (!open) return null;

  return (
    <div className="dialog-backdrop" role="presentation">
      <div
        className="form-dialog bulk-organization-dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="bulk-organization-title"
      >
        <div className="form-dialog-header">
          <div>
            <h2 id="bulk-organization-title">Organize selected links</h2>
            <p>Set a folder and comma-separated tags for {selectedCount} selected link{selectedCount === 1 ? "" : "s"}.</p>
          </div>
          <Button variant="ghost" className="dialog-close" aria-label="Close" onClick={onCancel}>×</Button>
        </div>
        <div className="form-dialog-body">
          <div className="field-group">
            <Label htmlFor="bulk-folder">Folder</Label>
            <Input
              id="bulk-folder"
              value={folder}
              onChange={(event) => setFolder(event.target.value)}
              placeholder="Leave blank to clear"
              maxLength={128}
              disabled={isSubmitting}
            />
          </div>
          <div className="field-group">
            <Label htmlFor="bulk-tags">Tags</Label>
            <Input
              id="bulk-tags"
              value={tags}
              onChange={(event) => setTags(event.target.value)}
              placeholder="launch, email"
              disabled={isSubmitting}
            />
            <span className="field-hint">Leave blank to clear tags. Duplicate tags are normalized.</span>
          </div>
        </div>
        <div className="dialog-actions">
          <Button variant="secondary" disabled={isSubmitting} onClick={onCancel}>Cancel</Button>
          <Button
            disabled={isSubmitting}
            onClick={() => onConfirm(folder, parseTagInput(tags))}
          >
            {isSubmitting ? "Applying..." : "Apply organization"}
          </Button>
        </div>
      </div>
    </div>
  );
}
