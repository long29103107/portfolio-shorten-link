import { useEffect, useState } from "react";
import { cancelShortLinkBulkJob, getShortLinkBulkJobStatus, submitShortLinkBulkJob } from "../api/shortLinksApi";
import type { ShortLinkBulkJobStatusResponse, ShortLinkBulkOperationRequest } from "../types";

export type BulkJobCenterEntry = {
  request: ShortLinkBulkOperationRequest;
  status: ShortLinkBulkJobStatusResponse;
};

const STORAGE_KEY = "shorten-link.bulk-jobs";
export const BULK_SELECTION_STORAGE_KEY = "shorten-link.bulk-selection";

export function useBulkJobCenter() {
  const [jobs, setJobs] = useState<BulkJobCenterEntry[]>(() => {
    try {
      return JSON.parse(localStorage.getItem(STORAGE_KEY) ?? "[]") as BulkJobCenterEntry[];
    } catch {
      return [];
    }
  });
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(jobs.slice(0, 20)));
  }, [jobs]);

  useEffect(() => {
    const activeJobs = jobs.filter((job) => !isTerminal(job.status));
    if (activeJobs.length === 0) return;
    const timer = window.setInterval(() => {
      void Promise.all(activeJobs.map(async (job) => {
        try {
          return await getShortLinkBulkJobStatus(job.status.jobId);
        } catch {
          return null;
        }
      })).then((updates) => {
        setJobs((current) => current.map((job) => {
          const index = activeJobs.findIndex((active) => active.status.jobId === job.status.jobId);
          return index >= 0 && updates[index] ? { ...job, status: updates[index]! } : job;
        }));
      });
    }, 1000);
    return () => window.clearInterval(timer);
  }, [jobs]);

  const submit = async (request: ShortLinkBulkOperationRequest) => {
    setError(null);
    try {
      const accepted = await submitShortLinkBulkJob({
        ...request,
        idempotencyKey: request.idempotencyKey ?? crypto.randomUUID()
      });
      const status: ShortLinkBulkJobStatusResponse = {
        ...accepted,
        processedCount: 0,
        succeededCount: 0,
        failedCount: 0,
        result: null,
        error: null
      };
      setJobs((current) => [{ request, status }, ...current.filter((job) => job.status.jobId !== status.jobId)]);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "The bulk job could not be submitted.");
    }
  };

  const cancel = async (jobId: string) => {
    try {
      const status = await cancelShortLinkBulkJob(jobId);
      setJobs((current) => current.map((job) => job.status.jobId === jobId ? { ...job, status } : job));
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "The bulk job could not be cancelled.");
    }
  };

  const retry = async (job: BulkJobCenterEntry) => {
    await submit({ ...job.request, idempotencyKey: crypto.randomUUID() });
  };

  return { jobs, error, submit, cancel, retry };
}

export const isTerminal = (status: ShortLinkBulkJobStatusResponse) =>
  status.status === "completed" || status.status === "failed" || status.status === "cancelled";
