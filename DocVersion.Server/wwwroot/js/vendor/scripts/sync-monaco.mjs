import { cpSync, existsSync, mkdirSync, rmSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);
const rootDir = resolve(__dirname, "../../../../..");

const sourceDir = resolve(
  rootDir,
  "node_modules",
  "monaco-editor",
  "min",
  "vs",
);
const targetDir = resolve(
  rootDir,
  "DocVersion.Server",
  "wwwroot",
  "js",
  "vendor",
  "monaco",
  "vs",
);

if (!existsSync(sourceDir)) {
  throw new Error(
    "Monaco source files not found. Run npm install before npm run sync:monaco.",
  );
}

rmSync(targetDir, { recursive: true, force: true });
mkdirSync(targetDir, { recursive: true });
cpSync(sourceDir, targetDir, { recursive: true });

console.log("Monaco assets synced to wwwroot/js/vendor/monaco/vs");
