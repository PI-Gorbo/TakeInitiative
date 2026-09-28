// Serves the browser harness for step 23a on http://localhost:3190 (not 3000/3100, which are the
// dev servers). Everything is same-origin: the page, the worker, onnxruntime-web's WASM, the
// tokenizer library, the fixture, web's score.ts (types stripped by Node) and the weights from
// `.data/models/` (run fetch-models.mjs first). The page is cross-origin isolated (COOP/COEP)
// so Chrome's `performance.measureUserAgentSpecificMemory()` works.
// Usage: node serve.mjs [port]
import { createReadStream } from "node:fs";
import { readFile, stat } from "node:fs/promises";
import { createServer } from "node:http";
import { stripTypeScriptTypes } from "node:module";
import { dirname, extname, join, normalize, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const repo = resolve(here, "../..");
const port = Number(process.argv[2] ?? 3190);
const types = { ".html": "text/html", ".mjs": "text/javascript", ".js": "text/javascript", ".json": "application/json", ".wasm": "application/wasm" };

const routes = [
    ["/models/", join(repo, ".data/models")],
    ["/ort/", join(here, "node_modules/onnxruntime-web/dist")],
    ["/tokenizers/", join(here, "node_modules/@huggingface/tokenizers/dist")],
    ["/lib/", join(here, "lib")],
];

createServer(async (req, res) => {
    const path = decodeURIComponent(new URL(req.url, "http://x").pathname);
    const headers = {
        "Cross-Origin-Opener-Policy": "same-origin",
        "Cross-Origin-Embedder-Policy": "require-corp",
        "Cache-Control": "no-store",
    };
    try {
        if (path === "/score.js") {
            const ts = await readFile(join(repo, "apps/TakeInitiative.Web/utils/extraction/spike/score.ts"), "utf8");
            res.writeHead(200, { ...headers, "Content-Type": "text/javascript" });
            return res.end(stripTypeScriptTypes(ts));
        }
        const fixed = {
            "/": join(here, "harness.html"),
            "/worker.mjs": join(here, "harness-worker.mjs"),
            "/models.mjs": join(here, "models.mjs"),
            "/notes.json": join(repo, "apps/TakeInitiative.Web/tests/fixtures/extraction/notes.json"),
        };
        let file = fixed[path];
        for (const [prefix, dir] of routes) {
            if (!file && path.startsWith(prefix)) {
                file = join(dir, normalize(path.slice(prefix.length)).replace(/^(\.\.[/\\])+/, ""));
            }
        }
        const info = file && (await stat(file).catch(() => null));
        if (!info?.isFile()) {
            res.writeHead(404, headers);
            return res.end("not found");
        }
        res.writeHead(200, { ...headers, "Content-Type": types[extname(file)] ?? "application/octet-stream", "Content-Length": info.size });
        createReadStream(file).pipe(res);
    } catch (e) {
        res.writeHead(500, headers);
        res.end(String(e));
    }
}).listen(port, () => console.log(`23a harness on http://localhost:${port}`));
