import type { ShortLinkAdminItem } from "./types";

export const shortLinkCsvHeaders = [
  "Code",
  "Short URL",
  "Destination URL",
  "Created At (UTC)",
  "Expires At (UTC)",
  "Status",
  "Access",
  "Created By"
] as const;

export function serializeShortLinksCsv(items: readonly ShortLinkAdminItem[]): string {
  const rows = items.map((link) => [
    link.code,
    link.shortUrl,
    link.originalUrl,
    link.createdAtUtc,
    link.expiredAtUtc ?? "",
    link.isActive ? "Active" : "Inactive",
    link.accessLevel ?? "",
    link.createdByDisplayName ?? link.createdByUsername ?? link.createdByUserId ?? ""
  ]);

  return [shortLinkCsvHeaders, ...rows]
    .map((row) => row.map(escapeCsvCell).join(","))
    .join("\r\n") + "\r\n";
}

export function downloadShortLinksCsv(items: readonly ShortLinkAdminItem[]): void {
  const blob = new Blob(["\uFEFF", serializeShortLinksCsv(items)], { type: "text/csv;charset=utf-8" });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = "shorten-links.csv";
  anchor.click();
  URL.revokeObjectURL(url);
}

function escapeCsvCell(value: string): string {
  return /[",\r\n]/.test(value) ? `"${value.replaceAll('"', '""')}"` : value;
}
