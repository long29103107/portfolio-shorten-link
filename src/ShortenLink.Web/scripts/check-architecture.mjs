import { existsSync, readdirSync, statSync } from "node:fs";
import { dirname, extname, join, relative, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";

const projectRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const sourceRoot = join(projectRoot, "src");
const violations = [];

function collectFiles(directory) {
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const path = join(directory, entry.name);
    if (entry.isDirectory()) return collectFiles(path);
    return [path];
  });
}

function resolveImport(importer, specifier) {
  const base = resolve(dirname(importer), specifier);
  const candidates = [base, `${base}.ts`, `${base}.tsx`, join(base, "index.ts"), join(base, "index.tsx")];
  return candidates.find((candidate) => existsSync(candidate) && (!extname(candidate) || statSync(candidate).isFile()));
}

function boundaryOf(path) {
  const parts = relative(sourceRoot, path).split(sep);
  if (parts[0] === "features") return { layer: "features", feature: parts[1] };
  return { layer: parts[0] };
}

for (const file of collectFiles(sourceRoot).filter((path) => [".ts", ".tsx"].includes(extname(path)))) {
  const importer = boundaryOf(file);
  const source = await import("node:fs").then(({ readFileSync }) => readFileSync(file, "utf8"));
  const imports = [...source.matchAll(/(?:from\s+|import\s*\()(["'])([^"']+)\1/g)].map((match) => match[2]);

  for (const specifier of imports.filter((value) => value.startsWith("."))) {
    const targetPath = resolveImport(file, specifier);
    if (!targetPath) continue;
    const target = boundaryOf(targetPath);
    const forbidden = importer.layer === "shared"
      ? target.layer === "app" || target.layer === "features"
      : importer.layer === "features"
        ? target.layer === "app"
          || (target.layer === "features" && target.feature !== importer.feature)
        : false;
    if (forbidden) {
      violations.push(`${relative(projectRoot, file)} -> ${specifier}`);
    }
  }
}

if (violations.length > 0) {
  console.error("Frontend architecture boundary violations:");
  for (const violation of violations) console.error(`- ${violation}`);
  process.exit(1);
}

console.log("Frontend architecture boundaries: OK");
