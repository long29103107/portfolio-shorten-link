import { existsSync, readdirSync, readFileSync, statSync } from "node:fs";
import { basename, dirname, extname, join, relative, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";

const projectRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const distRoot = resolve(projectRoot, "dist");
const assetsRoot = join(distRoot, "assets");

const budgets = {
  entryJsBytes: 260 * 1024,
  entryCssBytes: 60 * 1024,
  totalJsBytes: 380 * 1024,
  largestLazyChunkBytes: 80 * 1024
};

const requiredLazyChunks = [
  "AdminDashboardPage",
  "AuditLogPage",
  "SecurityManagementPage",
  "ShortLinkAdminPage",
  "ShortLinkDetailPage"
];

function collectFiles(directory) {
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const path = join(directory, entry.name);
    return entry.isDirectory() ? collectFiles(path) : [path];
  });
}

function toAssetPath(urlPath) {
  return join(distRoot, urlPath.replace(/^\/+/, "").split("/").join(sep));
}

function getAssetUrl(html, tagName, attribute) {
  const match = html.match(new RegExp(`<${tagName}[^>]+${attribute}=["']([^"']+)["']`, "i"));
  return match?.[1] ?? null;
}

function getStylesheetUrl(html) {
  const match = html.match(/<link[^>]+rel=["']stylesheet["'][^>]+href=["']([^"']+)["']/i);
  return match?.[1] ?? null;
}

function getSize(path) {
  return statSync(path).size;
}

function formatSize(bytes) {
  return `${(bytes / 1024).toFixed(1)} KiB`;
}

function formatBudget(bytes) {
  return formatSize(bytes);
}

function report(label, value, budget) {
  const result = value <= budget ? "PASS" : "FAIL";
  console.log(`${result} ${label}: ${formatSize(value)} / ${formatBudget(budget)}`);
  return value <= budget;
}

if (!existsSync(distRoot) || !existsSync(join(distRoot, "index.html")) || !existsSync(assetsRoot)) {
  console.error("Performance check failed: run the production build before checking dist output.");
  process.exit(1);
}

const indexHtml = readFileSync(join(distRoot, "index.html"), "utf8");
const entryJsUrl = getAssetUrl(indexHtml, "script", "src");
const entryCssUrl = getStylesheetUrl(indexHtml);
const failures = [];

if (!entryJsUrl) failures.push("index.html does not reference a module entry JavaScript asset.");
if (!entryCssUrl) failures.push("index.html does not reference a stylesheet asset.");

const entryJsPath = entryJsUrl ? toAssetPath(entryJsUrl) : null;
const entryCssPath = entryCssUrl ? toAssetPath(entryCssUrl) : null;

if (!entryJsPath || !existsSync(entryJsPath)) {
  failures.push(`Entry JavaScript asset is missing: ${entryJsUrl ?? "<unknown>"}`);
}

if (!entryCssPath || !existsSync(entryCssPath)) {
  failures.push(`Entry CSS asset is missing: ${entryCssUrl ?? "<unknown>"}`);
}

const jsAssets = collectFiles(assetsRoot).filter((path) => extname(path) === ".js");
const lazyJsAssets = jsAssets.filter((path) => path !== entryJsPath);
const totalJsBytes = jsAssets.reduce((total, path) => total + getSize(path), 0);
const largestLazyChunk = lazyJsAssets.reduce(
  (largest, path) => Math.max(largest, getSize(path)),
  0
);

console.log("Frontend performance budget");

if (entryJsPath && existsSync(entryJsPath) && !report("entry JavaScript", getSize(entryJsPath), budgets.entryJsBytes)) {
  failures.push(`Entry JavaScript exceeds ${formatBudget(budgets.entryJsBytes)}.`);
}

if (entryCssPath && existsSync(entryCssPath) && !report("entry CSS", getSize(entryCssPath), budgets.entryCssBytes)) {
  failures.push(`Entry CSS exceeds ${formatBudget(budgets.entryCssBytes)}.`);
}

if (!report("total JavaScript", totalJsBytes, budgets.totalJsBytes)) {
  failures.push(`Total JavaScript exceeds ${formatBudget(budgets.totalJsBytes)}.`);
}

if (!report("largest lazy chunk", largestLazyChunk, budgets.largestLazyChunkBytes)) {
  failures.push(`Largest lazy chunk exceeds ${formatBudget(budgets.largestLazyChunkBytes)}.`);
}

for (const requiredChunk of requiredLazyChunks) {
  const matchingAsset = lazyJsAssets.find((path) => basename(path).startsWith(`${requiredChunk}-`));
  if (!matchingAsset) {
    failures.push(`Required lazy route chunk is missing: ${requiredChunk}`);
    console.log(`FAIL lazy route chunk: ${requiredChunk} (missing)`);
    continue;
  }

  console.log(`PASS lazy route chunk: ${requiredChunk} (${formatSize(getSize(matchingAsset))})`);
}

if (failures.length > 0) {
  console.error("\nPerformance budget failed:");
  for (const failure of failures) console.error(`- ${failure}`);
  process.exit(1);
}

console.log(`\nPerformance budget passed for ${relative(projectRoot, distRoot)}.`);
