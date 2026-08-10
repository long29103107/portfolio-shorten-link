import { useState } from "react";
import { Button } from "@/shared/components/ui/button";
import type { ShortLinkBulkOperation, ShortLinkBulkOperationRequest } from "../types";
import { isTerminal, type BulkJobCenterEntry, useBulkJobCenter } from "../hooks/useBulkJobCenter";

type BulkJobCenterProps = {
  selectedCodes: string[];
};

export function BulkJobCenter({ selectedCodes }: BulkJobCenterProps) {
  const [operation, setOperation] = useState<ShortLinkBulkOperation>("deactivate");
  const { jobs, error, submit, cancel, retry } = useBulkJobCenter();
  const canSubmit = selectedCodes.length > 0;

  return (
    <section className="bulk-job-center" aria-label="Bulk jobs">
      <div className="bulk-job-center-header">
        <div>
          <p className="eyebrow">Background work</p>
          <h2>Bulk job center</h2>
          <p>Run selected links without holding the table request open.</p>
        </div>
        <div className="bulk-job-submit">
          <select value={operation} onChange={(event) => setOperation(event.target.value as ShortLinkBulkOperation)}>
            <option value="activate">Activate</option>
            <option value="deactivate">Deactivate</option>
            <option value="delete">Delete</option>
          </select>
          <Button disabled={!canSubmit} onClick={() => void submit({ codes: selectedCodes, operation })}>
            Submit {selectedCodes.length || "selected"}
          </Button>
        </div>
      </div>
      {!canSubmit ? <p className="muted-copy">Select links on the Short links page first to submit a new background job.</p> : null}
      {error ? <p className="recovery-banner recovery-banner-error" role="alert">{error}</p> : null}
      {jobs.length === 0 ? <p className="muted-copy">No background jobs yet.</p> : (
        <div className="bulk-job-list">
          {jobs.map((job) => <BulkJobRow key={job.status.jobId} job={job} onCancel={cancel} onRetry={retry} />)}
        </div>
      )}
    </section>
  );
}

function BulkJobRow({ job, onCancel, onRetry }: { job: BulkJobCenterEntry; onCancel: (id: string) => Promise<void>; onRetry: (job: BulkJobCenterEntry) => Promise<void> }) {
  const { status } = job;
  const percent = status.totalCount === 0 ? 0 : Math.round((status.processedCount / status.totalCount) * 100);
  return (
    <div className="bulk-job-row">
      <div>
        <strong>{job.request.operation}</strong>
        <span>{status.processedCount}/{status.totalCount} · {status.status}</span>
      </div>
      <progress max={100} value={percent} aria-label={`${percent}% complete`} />
      {!isTerminal(status) ? <Button variant="secondary" onClick={() => void onCancel(status.jobId)}>Cancel</Button> : null}
      {status.status === "failed" ? <Button variant="secondary" onClick={() => void onRetry(job)}>Retry</Button> : null}
      {status.error ? <small>{status.error}</small> : null}
    </div>
  );
}
