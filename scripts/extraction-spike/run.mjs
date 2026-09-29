// Step 23a's spike runner. Measures each candidate in its own Node process, on
// `onnxruntime-web` (WASM, one thread: what the browser worker will run) or
// `onnxruntime-node` (native CPU, one thread), over the invented test set, then scores.
//
//   node run.mjs                       every candidate, both backends, then the summary
//   node run.mjs --one <id> <backend>  one candidate in this process (the parent uses this)
//   node run.mjs --summary             score what is in .data/extraction-spike/
//
// Weights come from `.data/models/` (fetch-models.mjs). Results go to `.data/extraction-spike/`.
import { execFileSync } from "node:child_process";
import { createReadStream } from "node:fs";
import { mkdir, readFile, readdir, stat, writeFile } from "node:fs/promises";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { createGzip } from "node:zlib";
import { Tokenizer } from "@huggingface/tokenizers";
import { candidates } from "./models.mjs";
import { latencyBySize, makeExtractor, scoreRun } from "./lib/extract.mjs";
import { scoreNotes } from "../../apps/TakeInitiative.Web/utils/extraction/spike/score.ts";

const here = dirname(fileURLToPath(import.meta.url));
const repo = resolve(here, "../..");
const modelsDir = join(repo, ".data/models");
const outDir = join(repo, ".data/extraction-spike");
const notes = JSON.parse(await readFile(join(repo, "apps/TakeInitiative.Web/tests/fixtures/extraction/notes.json"), "utf8"));
const args = process.argv.slice(2);
const ms = (t0) => performance.now() - t0;
async function loadOrt(backend) {
    if (backend === "wasm") {
        const ort = await import("onnxruntime-web");
        ort.env.wasm.numThreads = 1;
        return { ort, options: { executionProviders: ["wasm"] } };
    }
    const ort = await import("onnxruntime-node");
    return { ort, options: { executionProviders: ["cpu"], intraOpNumThreads: 1, interOpNumThreads: 1 } };
}

async function one(id, backend, onlyNotes) {
    const c = candidates.find((x) => x.id === id);
    const dir = join(modelsDir, c.repo, c.revision);
    const rssBase = process.memoryUsage().rss;
    const { ort, options } = await loadOrt(backend);

    let t0 = performance.now();
    const bytes = new Uint8Array(await readFile(join(dir, c.model)));
    const tokenizerJson = JSON.parse(await readFile(join(dir, c.tokenizer), "utf8"));
    const tokenizerConfig = JSON.parse(await readFile(join(dir, c.tokenizerConfig), "utf8"));
    const config = JSON.parse(await readFile(join(dir, c.extra[0]), "utf8"));
    const readMs = ms(t0);

    t0 = performance.now();
    const tokenizer = new Tokenizer(tokenizerJson, tokenizerConfig);
    let session = await ort.InferenceSession.create(bytes, options);
    const coldMs = readMs + ms(t0);
    await session.release?.();
    t0 = performance.now();
    session = await ort.InferenceSession.create(bytes, options);
    const warmMs = ms(t0);

    const extract = makeExtractor({ ort, session, tokenizer, config });

    const run = onlyNotes ? notes.filter((n) => onlyNotes.includes(n.id)) : notes;
    await extract(notes.find((n) => n.size === "typical").text); // warm-up
    const perNote = [];
    for (const n of run) {
        t0 = performance.now();
        const spans = await extract(n.text);
        perNote.push({ id: n.id, size: n.size, ms: ms(t0), spans });
    }
    const latency = latencyBySize(perNote);
    const result = {
        id,
        backend,
        runtime: backend === "wasm" ? `onnxruntime-web ${ort.env.versions?.web ?? ""}` : `onnxruntime-node ${ort.env.versions?.node ?? ""}`,
        threads: 1,
        readMs,
        coldMs,
        warmMs,
        rssBaseMB: rssBase / 2 ** 20,
        rssPeakMB: process.resourceUsage().maxRSS / 1024,
        latency,
        perNote,
    };
    await mkdir(outDir, { recursive: true });
    await writeFile(join(outDir, `${id}.${backend}.json`), JSON.stringify(result));
    console.log(JSON.stringify({ id, backend, coldMs, warmMs, rssPeakMB: result.rssPeakMB, latency }));
}

async function gzipSize(path) {
    let n = 0;
    await new Promise((ok, fail) =>
        createReadStream(path).pipe(createGzip({ level: 6 })).on("data", (d) => (n += d.length)).on("end", ok).on("error", fail)
    );
    return n;
}

async function sizes() {
    const out = {};
    const manifest = JSON.parse(await readFile(join(modelsDir, "manifest.json"), "utf8"));
    const wasm = join(here, "node_modules/onnxruntime-web/dist/ort-wasm-simd-threaded.wasm");
    const wasmBytes = (await stat(wasm)).size;
    const wasmGz = await gzipSize(wasm);
    for (const c of candidates) {
        const files = [c.model, c.tokenizer, c.tokenizerConfig, c.extra[0]];
        let raw = wasmBytes;
        let gz = wasmGz;
        for (const f of files) {
            raw += manifest[c.id].files[f].bytes;
            gz += await gzipSize(join(modelsDir, c.repo, c.revision, f));
        }
        out[c.id] = { raw, gz, wasm: wasmBytes, model: manifest[c.id].files[c.model].bytes };
    }
    return out;
}

async function summary() {
    const files = (await readdir(outDir)).filter((f) => f.endsWith(".json") && !f.startsWith("summary"));
    const size = await sizes();
    const rows = [];
    for (const f of files.sort()) {
        const r = JSON.parse(await readFile(join(outDir, f), "utf8"));
        const sweep = [];
        for (let t = 0.3; t <= 0.951; t += 0.05) sweep.push(scoreRun(scoreNotes, notes, r.perNote, Math.round(t * 100) / 100));
        const best = sweep.reduce((b, s) => (s.exact.f1 > b.exact.f1 ? s : b));
        const at05 = sweep.find((s) => s.threshold === 0.5);
        rows.push({
            id: r.id,
            backend: r.backend,
            notes: r.perNote.length,
            size: size[r.id],
            subset: r.perNote.length < notes.length,
            coldMs: r.coldMs,
            warmMs: r.warmMs,
            rssPeakMB: r.rssPeakMB,
            latency: r.latency,
            at05: brief(at05),
            best: brief(best),
            sweep: sweep.map(brief),
        });
    }
    await writeFile(join(outDir, "summary.json"), JSON.stringify(rows, null, 2));
    for (const r of rows) {
        const L = r.latency;
        console.log(
            [
                r.id.padEnd(24),
                r.backend.padEnd(6),
                `n=${r.notes}`,
                `dl ${(r.size.raw / 1e6).toFixed(0)}/${(r.size.gz / 1e6).toFixed(0)}MB`,
                `load ${(r.coldMs / 1000).toFixed(1)}/${(r.warmMs / 1000).toFixed(1)}s`,
                `rss ${r.rssPeakMB.toFixed(0)}MB`,
                ...["short", "typical", "long"].map((s) => (L[s] ? `${s[0]} ${L[s].p50.toFixed(0)}/${L[s].p95.toFixed(0)}ms` : `${s[0]} -`)),
                `@0.5 F1 ${r.at05.exactF1}/${r.at05.overlapF1} top3 ${r.at05.top3}`,
                `best@${r.best.threshold} F1 ${r.best.exactF1}/${r.best.overlapF1} P ${r.best.exactP} R ${r.best.exactR} kind ${r.best.kind} top3 ${r.best.top3}`,
            ].join("  ")
        );
    }
}

const f2 = (x) => (x == null ? null : Math.round(x * 1000) / 1000);
const brief = (s) => ({
    threshold: s.threshold,
    exactP: f2(s.exact.precision),
    exactR: f2(s.exact.recall),
    exactF1: f2(s.exact.f1),
    overlapP: f2(s.overlap.precision),
    overlapR: f2(s.overlap.recall),
    overlapF1: f2(s.overlap.f1),
    kind: f2(s.kindAccuracy),
    top3: f2(s.top3.precision),
    top3Shown: s.top3.shown,
});

if (args[0] === "--one") {
    await one(args[1], args[2], args[3]?.split(","));
} else if (args[0] === "--summary") {
    await summary();
} else {
    const plan = args.length ? args : candidates.flatMap((c) => [`${c.id}:native`, `${c.id}:wasm`]);
    for (const p of plan) {
        const [id, backend, subset] = p.split(":");
        console.error("running", id, backend, subset ?? "");
        execFileSync(process.execPath, [fileURLToPath(import.meta.url), "--one", id, backend, ...(subset ? [subset] : [])], { stdio: "inherit" });
    }
    await summary();
}
