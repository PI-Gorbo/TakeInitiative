// Step 23b: the suggestion model's worker. Created by `useExtractor().ensure()` only, so the
// runtime (`onnxruntime-web`, WASM, one thread) and the tokenizer are in this chunk and no page's.
// It loads the model's files from the Cache API or the network (checked by sha256 before they are
// cached or used), then extracts one note at a time; `useExtractor` keeps the queue.
// Note text never leaves this worker: it only posts spans back to the page.

import * as ort from "onnxruntime-web/wasm";
import wasmUrl from "onnxruntime-web/ort-wasm-simd-threaded.wasm?url";
import { Tokenizer } from "@huggingface/tokenizers";
import { createExtractor } from "~/utils/extraction/extractor";
import { ChecksumError, MODEL_CACHE, getModelFile } from "~/utils/extraction/modelCache";
import type { FromWorker, ToWorker } from "~/utils/extraction/messages";

// The runtime's WASM is one of the app's own build assets, never the runtime's default CDN.
ort.env.wasm.wasmPaths = { wasm: new URL(wasmUrl, self.location.href).href };
// Threads need cross-origin isolation (COOP/COEP), which the app does not turn on.
ort.env.wasm.numThreads = 1;

const scope = self as unknown as {
    postMessage(message: FromWorker): void;
    onmessage: ((event: MessageEvent<ToWorker>) => void) | null;
};
const post = (m: FromWorker) => scope.postMessage(m);

let extractor: ReturnType<typeof createExtractor> | null = null;

async function load(source: Extract<ToWorker, { type: "load" }>["source"]) {
    const started = performance.now();
    const loaded = new Map<string, number>();
    let fromCache = true;
    const files: Record<string, Uint8Array> = {};
    for (const file of source.files) {
        fromCache &&= !!(await (await caches.open(MODEL_CACHE)).match(file.url));
        files[file.path] = await getModelFile(file, {
            caches,
            fetch: (url, init) => fetch(url, init),
            onProgress: (n) => {
                loaded.set(file.path, n);
                post({ type: "progress", loaded: [...loaded.values()].reduce((a, b) => a + b, 0), total: source.bytes, fromCache });
            },
        });
    }
    const json = (path: string) => JSON.parse(new TextDecoder().decode(files[path]));
    const model = source.files.find((f) => f.path.endsWith(".onnx"))!.path;
    const tokenizer = new Tokenizer(json("tokenizer.json"), json("tokenizer_config.json"));
    const session = await ort.InferenceSession.create(files[model], { executionProviders: ["wasm"] });
    extractor = createExtractor({
        ort: ort as never,
        session: session as never,
        tokenizer: tokenizer as never,
        maxWidth: source.maxWidth,
        maxWords: source.maxWords,
        maxTokens: source.maxTokens,
        threshold: source.threshold,
    });
    post({ type: "ready", fromCache, ms: performance.now() - started });
}

// One message at a time: an extract waits for the load, and for the extract before it.
let chain: Promise<void> = Promise.resolve();

scope.onmessage = ({ data }) => {
    chain = chain.then(async () => {
        if (data.type === "load") {
            try {
                await load(data.source);
            } catch (e) {
                post({ type: "error", message: e instanceof Error ? e.message : String(e), checksum: e instanceof ChecksumError });
            }
            return;
        }
        if (!extractor) {
            post({ type: "error", id: data.id, message: "The suggestion model is not loaded." });
            return;
        }
        try {
            post({ type: "result", id: data.id, spans: await extractor.extract(data.text) });
        } catch (e) {
            post({ type: "error", id: data.id, message: e instanceof Error ? e.message : String(e) });
        }
    });
};
