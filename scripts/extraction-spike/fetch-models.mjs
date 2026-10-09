// Downloads the 23a candidates' files into `.data/models/{repo}/{revision}/` (git-ignored), and
// prints each file's size, sha256 and download time as JSON (`.data/models/manifest.json`).
// Usage: node fetch-models.mjs [candidate-id…]
import { createHash } from "node:crypto";
import { createWriteStream } from "node:fs";
import { mkdir, stat, writeFile, readFile } from "node:fs/promises";
import { dirname, join, resolve } from "node:path";
import { Readable } from "node:stream";
import { pipeline } from "node:stream/promises";
import { fileURLToPath } from "node:url";
import { candidates, filesOf } from "./models.mjs";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../.data/models");
const only = process.argv.slice(2);

async function sha256(path) {
    const hash = createHash("sha256");
    for await (const chunk of (await import("node:fs")).createReadStream(path)) hash.update(chunk);
    return hash.digest("hex");
}

const manifestPath = join(root, "manifest.json");
const manifest = JSON.parse(await readFile(manifestPath, "utf8").catch(() => "{}"));

for (const c of candidates.filter((c) => only.length === 0 || only.includes(c.id))) {
    manifest[c.id] = manifest[c.id] ?? { repo: c.repo, revision: c.revision, files: {} };
    for (const file of filesOf(c)) {
        const path = join(root, c.repo, c.revision, file);
        const have = await stat(path).catch(() => null);
        let seconds = manifest[c.id].files[file]?.seconds ?? null;
        if (!have) {
            await mkdir(dirname(path), { recursive: true });
            const url = `https://huggingface.co/${c.repo}/resolve/${c.revision}/${file}`;
            const started = performance.now();
            const res = await fetch(url);
            if (!res.ok) throw new Error(`${url}: ${res.status}`);
            await pipeline(Readable.fromWeb(res.body), createWriteStream(path + ".part"));
            await (await import("node:fs/promises")).rename(path + ".part", path);
            seconds = (performance.now() - started) / 1000;
        }
        const bytes = (await stat(path)).size;
        manifest[c.id].files[file] = { bytes, sha256: await sha256(path), seconds };
        console.log(c.id, file, bytes, seconds?.toFixed(1) ?? "cached");
    }
}

await writeFile(manifestPath, JSON.stringify(manifest, null, 2));
console.log("wrote", manifestPath);
