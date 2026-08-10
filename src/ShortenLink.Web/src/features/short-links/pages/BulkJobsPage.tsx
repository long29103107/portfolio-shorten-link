import { useState } from "react";
import { BulkJobCenter } from "../components/BulkJobCenter";
import { BULK_SELECTION_STORAGE_KEY } from "../hooks/useBulkJobCenter";

function readSelectedCodes(): string[] {
  try {
    const stored = JSON.parse(sessionStorage.getItem(BULK_SELECTION_STORAGE_KEY) ?? "[]");
    return Array.isArray(stored) ? stored.filter((code): code is string => typeof code === "string") : [];
  } catch {
    return [];
  }
}

export function BulkJobsPage() {
  const [selectedCodes] = useState(readSelectedCodes);

  return (
    <div className="bulk-jobs-page">
      <BulkJobCenter selectedCodes={selectedCodes} />
    </div>
  );
}
