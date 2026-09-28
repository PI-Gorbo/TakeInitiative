// Step 23b: puts the suggestion model's files where the web app serves them,
// `apps/TakeInitiative.Web/public/models/{id}/{revision}/` (git-ignored), and refuses any file
// whose sha256 differs from `apps/TakeInitiative.Web/suggestion-model.json`.
//
//   pnpm models:fetch [--from <dir-or-url>] [--out <dir>] [--if-missing] [--soft]
//
// Where the bytes come from, first that exists:
//   1. `--from` or `SUGGESTIONS_MODEL_FROM` (a folder, or a base URL the file names are added to);
//   2. `.data/models/export/{id}/{revision}/`, what `uv run scripts/gliner/export.py` writes;
//   3. the manifest's `mirror` (a Hugging Face repo at a pinned revision whose files are byte
//      for byte what the export produces, checked here by sha256 like any other source).
// `--if-missing` does nothing when the folder is already complete (for `pnpm dev`).
// `--soft` warns instead of failing (for `pnpm dev`, which must start offline).
// `LICENSE` and `NOTICE` are copied from `scripts/models/{id}/`, which is committed.
import { createHash } from "node:crypto";
import { createReadStream, createWriteStream, existsSync } from "node:fs";
import { copyFile, mkdir, readFile, rename, rm, stat } from "node:fs/promises";
import { dirname, join, resolve } from "node:path";
import { Readable, Transform } from "node:stream";
import { pipeline } from "node:stream/promises";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const repo = resolve(here, "../..");
const manifest = JSON.parse(await readFile(join(repo, "apps/TakeInitiative.Web/suggestion-model.json"), "utf8"));

const args = process.argv.slice(2);
const flag = (name) => args.includes(name);
const option = (name) => {
    const i = args.indexOf(name);
    return i >= 0 ? args[i + 1] : undefined;
};

const outRoot = resolve(option("--out") ?? join(repo, "apps/TakeInitiative.Web/public/models"));
const out = join(outRoot, manifest.id, manifest.revision);
const exported = join(repo, ".data/models/export", manifest.id, manifest.revision);
const from = option("--from") ?? process.env.SUGGESTIONS_MODEL_FROM;

async function sha256(path) {
    const hash = createHash("sha256");
    for await (const chunk of createReadStream(path)) hash.update(chunk);
    return hash.digest("hex");
}

async function matches(path, file) {
    const s = await stat(path).catch(() => null);
    return !!s && s.size === file.bytes && (await sha256(path)) === file.sha256;
}

function sourceOf(file) {
    if (from) return /^https?:\/\//.test(from) ? `${from.replace(/\/$/, "")}/${file.path}` : join(resolve(from), file.path);
    if (existsSync(join(exported, file.path))) return join(exported, file.path);
    return `${manifest.mirror}/${file.path}`;
}

async function place(file) {
    const target = join(out, file.path);
    if (await matches(target, file)) {
        console.log(`ok       ${file.path}`);
        return;
    }
    const source = sourceOf(file);
    const part = `${target}.part`;
    await mkdir(dirname(target), { recursive: true });
    if (/^https?:\/\//.test(source)) {
        const res = await fetch(source);
        if (!res.ok || !res.body) throw new Error(`${source}: HTTP ${res.status}`);
        let got = 0;
        let shown = 0;
        const count = new Transform({
            transform(chunk, _enc, done) {
                got += chunk.length;
                if (got - shown > 16 * 1024 * 1024) {
                    shown = got;
                    process.stdout.write(`         ${file.path} ${(got / 1e6).toFixed(0)} of ${(file.bytes / 1e6).toFixed(0)} MB\r`);
                }
                done(null, chunk);
            },
        });
        await pipeline(Readable.fromWeb(res.body), count, createWriteStream(part));
    } else {
        await copyFile(source, part);
    }
    if (!(await matches(part, file))) {
        await rm(part, { force: true });
        throw new Error(`${file.path} from ${source} does not match the pinned sha256 ${file.sha256}; refusing it`);
    }
    await rename(part, target);
    console.log(`fetched  ${file.path}  (${source})`);
}

try {
    const complete = async () => {
        for (const f of manifest.files) {
            const s = await stat(join(out, f.path)).catch(() => null);
            if (!s || s.size !== f.bytes) return false;
        }
        return true;
    };
    if (flag("--if-missing") && (await complete())) {
        process.exit(0);
    }
    console.log(`${manifest.name} → ${out}`);
    for (const file of manifest.files) await place(file);
    for (const name of ["LICENSE", "NOTICE"]) await copyFile(join(here, manifest.id, name), join(out, name));
    console.log("done: every file matches its pinned sha256");
} catch (e) {
    if (flag("--soft")) {
        console.warn(`models:fetch skipped: ${e.message}. Suggestions will not load until \`pnpm models:fetch\` succeeds.`);
        process.exit(0);
    }
    console.error(e.message);
    process.exit(1);
}
