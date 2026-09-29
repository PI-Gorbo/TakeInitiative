// The 23a harness's worker: loads one candidate on onnxruntime-web (WASM or WebGPU), timing the
// download (or the Cache API read) and session creation, then runs the invented notes once each
// after a warm-up and posts the per-note spans and latencies back.
import * as ort from "/ort/ort.all.min.mjs";
import { Tokenizer } from "/tokenizers/tokenizers.min.mjs";
import { candidates } from "/models.mjs";
import { makeExtractor } from "/lib/extract.mjs";

const CACHE = "ti-spike-23a";
ort.env.wasm.wasmPaths = "/ort/";
ort.env.wasm.numThreads = 1;

async function getFile(url, progress) {
    const cache = await caches.open(CACHE);
    const hit = await cache.match(url);
    if (hit) return { bytes: new Uint8Array(await hit.arrayBuffer()), cached: true };
    const res = await fetch(url);
    if (!res.ok) throw new Error(`${url}: ${res.status}`);
    const total = Number(res.headers.get("Content-Length") ?? 0);
    const reader = res.body.getReader();
    const parts = [];
    let got = 0;
    for (;;) {
        const { done, value } = await reader.read();
        if (done) break;
        parts.push(value);
        got += value.length;
        progress(url, got, total);
    }
    const bytes = new Uint8Array(got);
    let at = 0;
    for (const p of parts) {
        bytes.set(p, at);
        at += p.length;
    }
    await cache.put(url, new Response(bytes, { headers: { "Content-Type": "application/octet-stream" } }));
    return { bytes, cached: false };
}

self.onmessage = async ({ data }) => {
    try {
        if (data.type === "clear") {
            await caches.delete(CACHE);
            return self.postMessage({ type: "log", text: "cache cleared" });
        }
        const c = candidates.find((x) => x.id === data.id);
        const base = `/models/${c.repo}/${c.revision}/`;
        const progress = (url, got, total) => self.postMessage({ type: "progress", url, got, total });
        let t0 = performance.now();
        const files = {};
        let fromCache = true;
        let bytes = 0;
        for (const f of [c.model, c.tokenizer, c.tokenizerConfig, c.extra[0]]) {
            const r = await getFile(base + f, progress);
            files[f] = r.bytes;
            fromCache &&= r.cached;
            bytes += r.bytes.length;
        }
        const fetchMs = performance.now() - t0;
        const dec = (b) => JSON.parse(new TextDecoder().decode(b));
        t0 = performance.now();
        const tokenizer = new Tokenizer(dec(files[c.tokenizer]), dec(files[c.tokenizerConfig]));
        const session = await ort.InferenceSession.create(files[c.model], { executionProviders: [data.backend] });
        const createMs = performance.now() - t0;
        self.postMessage({ type: "loaded", fromCache, bytes, fetchMs, createMs, loadMs: fetchMs + createMs });

        const extract = makeExtractor({ ort, session, tokenizer, config: dec(files[c.extra[0]]) });
        const notes = data.notes;
        await extract(notes.find((n) => n.size === "typical").text);
        const perNote = [];
        for (const n of notes) {
            t0 = performance.now();
            const spans = await extract(n.text);
            perNote.push({ id: n.id, size: n.size, ms: performance.now() - t0, spans });
            self.postMessage({ type: "note", id: n.id, ms: perNote.at(-1).ms, done: perNote.length, of: notes.length });
        }
        self.postMessage({ type: "done", perNote });
    } catch (e) {
        self.postMessage({ type: "error", text: String(e?.stack ?? e) });
    }
};
