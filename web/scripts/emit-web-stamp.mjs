import { execSync } from "node:child_process";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const webRoot = join(dirname(fileURLToPath(import.meta.url)), "..");
const repoRoot = join(webRoot, "..");

function productVersion() {
  const props = readFileSync(join(repoRoot, "Directory.Build.props"), "utf8");
  const stamp = props.match(/<Version>([^<]*)<\/Version>/);
  if (!stamp) {
    throw new Error("Directory.Build.props has no <Version> element to stamp");
  }
  return stamp[1].trim();
}

function webSha() {
  try {
    return execSync("git rev-parse --short HEAD", {
      cwd: repoRoot,
      stdio: ["ignore", "pipe", "ignore"],
    })
      .toString()
      .trim();
  } catch {
    return "dev";
  }
}

const stamp = {
  productVersion: productVersion(),
  webSha: webSha(),
  builtAt: new Date().toISOString(),
};

const outDir = join(webRoot, "out");
mkdirSync(outDir, { recursive: true });
const outFile = join(outDir, "grimoire-web.json");
writeFileSync(outFile, `${JSON.stringify(stamp, null, 2)}\n`);

console.log(
  `web stamp: ${stamp.productVersion} @ ${stamp.webSha} built ${stamp.builtAt}`,
);
