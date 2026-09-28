import { createHash } from "node:crypto";
import { beforeEach, describe, expect, it } from "vitest";
import {
    ChecksumError,
    MODEL_CACHE,
    MODEL_INDEX_KEY,
    getModelFile,
    hasRoomFor,
    isCached,
    pruneModelCache,
    readCachedIndex,
    removeModelCache,
    writeCachedIndex,
    type CacheLike,
    type CachesLike,
    type StorageLike,
} from "~/utils/extraction/modelCache";
import { resolveModelSource, type SuggestionsConfig } from "~/utils/extraction/modelSource";

class FakeCache implements CacheLike {
    entries = new Map<string, Uint8Array>();
    async match(url: string) {
        const b = this.entries.get(url);
        return b ? new Response(b as BlobPart) : undefined;
    }
    async put(url: string, res: Response) {
        this.entries.set(url, new Uint8Array(await res.arrayBuffer()));
    }
    async delete(url: string) {
        return this.entries.delete(url);
    }
    async keys() {
        return [...this.entries.keys()].map((url) => ({ url }));
    }
}
class FakeCaches implements CachesLike {
    stores = new Map<string, FakeCache>();
    async open(name: string) {
        if (!this.stores.has(name)) this.stores.set(name, new FakeCache());
        return this.stores.get(name)!;
    }
    async delete(name: string) {
        return this.stores.delete(name);
    }
}
class FakeStorage implements StorageLike {
    map = new Map<string, string>();
    getItem = (k: string) => this.map.get(k) ?? null;
    setItem = (k: string, v: string) => void this.map.set(k, v);
    removeItem = (k: string) => void this.map.delete(k);
}

const bytesOf = (s: string) => new TextEncoder().encode(s);
const sha = (b: Uint8Array) => createHash("sha256").update(b).digest("hex");
const MODEL = bytesOf("pretend onnx weights");
const TOKENIZER = bytesOf('{"pretend":"tokenizer"}');

const config = (revision: string, baseUrl = ""): SuggestionsConfig => ({
    id: "gliner_small-v2.5",
    name: "GLiNER small v2.5",
    version: `x@${revision}`,
    revision,
    upstream: "https://example.test",
    licence: "Apache-2.0",
    attribution: "GLiNER",
    threshold: 0.4,
    maxWidth: 12,
    maxWords: 768,
    maxTokens: 512,
    files: [
        { path: "model_quantized.onnx", bytes: MODEL.length, sha256: sha(MODEL) },
        { path: "tokenizer.json", bytes: TOKENIZER.length, sha256: sha(TOKENIZER) },
    ],
    baseUrl,
});

/** A fetch that serves `body` in two chunks, optionally failing midway. */
function fakeFetch(bodies: Record<string, Uint8Array>, { failAfterFirstChunk = false } = {}) {
    const calls: string[] = [];
    const fn = async (url: string) => {
        calls.push(url);
        const path = url.split("/").pop()!;
        const body = bodies[path];
        if (!body) return new Response(null, { status: 404 });
        const half = Math.ceil(body.length / 2);
        const stream = new ReadableStream<Uint8Array>({
            start(c) {
                c.enqueue(body.slice(0, half));
                if (failAfterFirstChunk) c.error(new Error("network lost"));
                else {
                    c.enqueue(body.slice(half));
                    c.close();
                }
            },
        });
        return new Response(stream);
    };
    return Object.assign(fn, { calls });
}

let caches: FakeCaches;
beforeEach(() => {
    caches = new FakeCaches();
});

describe("resolveModelSource", () => {
    it("self-hosts under /models/{id}/{revision}/ by default", () => {
        const s = resolveModelSource(config("rev1"), "https://app.test");
        expect(s.selfHosted).toBe(true);
        expect(s.files[0].url).toBe("https://app.test/models/gliner_small-v2.5/rev1/model_quantized.onnx");
        expect(s.bytes).toBe(MODEL.length + TOKENIZER.length);
    });

    it("uses the base URL when one is set", () => {
        const s = resolveModelSource(config("rev1", "https://huggingface.co/org/repo/resolve/abc"), "https://app.test");
        expect(s.selfHosted).toBe(false);
        expect(s.host).toBe("huggingface.co");
        expect(s.files[1].url).toBe("https://huggingface.co/org/repo/resolve/abc/tokenizer.json");
    });
});

describe("getModelFile", () => {
    it("downloads, checks and caches by full URL (which holds the revision)", async () => {
        const source = resolveModelSource(config("rev1"), "https://app.test");
        const fetch = fakeFetch({ "model_quantized.onnx": MODEL, "tokenizer.json": TOKENIZER });
        const progress: number[] = [];
        for (const f of source.files) await getModelFile(f, { caches, fetch, onProgress: (n) => progress.push(n) });
        expect(await isCached(caches, source)).toBe(true);
        expect([...(await caches.open(MODEL_CACHE)).entries.keys()]).toEqual(source.files.map((f) => f.url));
        expect(progress.at(-1)).toBe(TOKENIZER.length);

        // A second load reads the cache: no network.
        const again = await getModelFile(source.files[0], { caches, fetch });
        expect(new TextDecoder().decode(again)).toBe("pretend onnx weights");
        expect(fetch.calls).toHaveLength(2);

        // A new revision is a new key.
        const next = resolveModelSource(config("rev2"), "https://app.test");
        expect(await isCached(caches, next)).toBe(false);
    });

    it("never stores a file whose checksum differs", async () => {
        const source = resolveModelSource(config("rev1"), "https://app.test");
        const tampered = bytesOf("pretend onnx weightz");
        const fetch = fakeFetch({ "model_quantized.onnx": tampered });
        await expect(getModelFile(source.files[0], { caches, fetch })).rejects.toBeInstanceOf(ChecksumError);
        expect((await caches.open(MODEL_CACHE)).entries.size).toBe(0);
    });

    it("never stores a partial download", async () => {
        const source = resolveModelSource(config("rev1"), "https://app.test");
        const fetch = fakeFetch({ "model_quantized.onnx": MODEL }, { failAfterFirstChunk: true });
        await expect(getModelFile(source.files[0], { caches, fetch })).rejects.toThrow("network lost");
        expect((await caches.open(MODEL_CACHE)).entries.size).toBe(0);
    });

    it("never stores an aborted download", async () => {
        const source = resolveModelSource(config("rev1"), "https://app.test");
        const controller = new AbortController();
        controller.abort();
        const fetch = async (_url: string, init?: RequestInit) => {
            if (init?.signal?.aborted) throw new DOMException("aborted", "AbortError");
            return new Response(MODEL as BlobPart);
        };
        await expect(getModelFile(source.files[0], { caches, fetch, signal: controller.signal })).rejects.toThrow("aborted");
        expect((await caches.open(MODEL_CACHE)).entries.size).toBe(0);
    });

    it("rejects an HTTP error", async () => {
        const source = resolveModelSource(config("rev1"), "https://app.test");
        await expect(getModelFile(source.files[0], { caches, fetch: fakeFetch({}) })).rejects.toThrow("HTTP 404");
    });
});

describe("pruning and removing", () => {
    it("drops other revisions' files and keeps this one's", async () => {
        const old = resolveModelSource(config("rev1"), "https://app.test");
        const now = resolveModelSource(config("rev2"), "https://app.test");
        const fetch = fakeFetch({ "model_quantized.onnx": MODEL, "tokenizer.json": TOKENIZER });
        for (const f of [...old.files, ...now.files]) await getModelFile(f, { caches, fetch });
        expect(await pruneModelCache(caches, now)).toBe(2);
        expect(await isCached(caches, now)).toBe(true);
        expect(await isCached(caches, old)).toBe(false);
    });

    it("removes the cache and the index", async () => {
        const storage = new FakeStorage();
        const source = resolveModelSource(config("rev1"), "https://app.test");
        await getModelFile(source.files[0], { caches, fetch: fakeFetch({ "model_quantized.onnx": MODEL }) });
        writeCachedIndex(storage, source);
        expect(readCachedIndex(storage)).toEqual({ id: source.id, revision: "rev1", bytes: source.bytes });
        await removeModelCache(caches, storage);
        expect(caches.stores.has(MODEL_CACHE)).toBe(false);
        expect(storage.getItem(MODEL_INDEX_KEY)).toBeNull();
    });

    it("ignores a malformed index", () => {
        const storage = new FakeStorage();
        storage.setItem(MODEL_INDEX_KEY, "{not json");
        expect(readCachedIndex(storage)).toBeNull();
        storage.setItem(MODEL_INDEX_KEY, JSON.stringify({ id: "x" }));
        expect(readCachedIndex(storage)).toBeNull();
    });
});

describe("hasRoomFor", () => {
    it("needs twice the download free", () => {
        expect(hasRoomFor(100, { quota: 1000, usage: 800 })).toBe(true);
        expect(hasRoomFor(100, { quota: 1000, usage: 801 })).toBe(false);
        expect(hasRoomFor(100, null)).toBe(true);
    });
});
