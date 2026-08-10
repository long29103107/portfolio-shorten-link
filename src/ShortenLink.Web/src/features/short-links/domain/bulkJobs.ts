import { getShortLinkBulkJobStatus } from "../api/shortLinksApi";
import type { ShortLinkBulkJobStatusResponse } from "../types";

export const isShortLinkBulkJobTerminal = (status: ShortLinkBulkJobStatusResponse) =>
  status.status === "completed" || status.status === "failed";

export async function waitForShortLinkBulkJob(
  jobId: string,
  options: { intervalMs?: number; signal?: AbortSignal } = {}
): Promise<ShortLinkBulkJobStatusResponse> {
  const intervalMs = options.intervalMs ?? 500;
  while (true) {
    const status = await getShortLinkBulkJobStatus(jobId, options.signal);
    if (isShortLinkBulkJobTerminal(status)) return status;
    await new Promise<void>((resolve, reject) => {
      const timer = window.setTimeout(resolve, intervalMs);
      options.signal?.addEventListener("abort", () => {
        window.clearTimeout(timer);
        reject(options.signal?.reason ?? new DOMException("Aborted", "AbortError"));
      }, { once: true });
    });
  }
}
