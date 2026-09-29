// Step 23b: the suggestion model's files in the Cache API (cache `ti-models-v1`, keyed by the
// full URL, which holds the revision), plus a small index in localStorage (`ti.model.cached`) so
// the Me page can show the space used without opening the cache. Everything is passed in (the
// cache storage, fetch, the digest), so the tests run with fakes and the worker with the real ones.
// The service worker never touches this cache (`public/sw.js` precaches nothing).

import type { ModelSource, ResolvedFile } from "./modelSource";

export const MODEL_CACHE = "ti-models-v1";
export const MODEL_INDEX_KEY = "ti.model.cached";

/** The parts of `CacheStorage` used here. */
export interface CachesLike {
    open(name: string): Promise<CacheLike>;
    delete(name: string): Promise<boolean>;
}
export interface CacheLike {
    match(url: string): Promise<Response | undefined>;
    put(url: string, response: Response): Promise<void>;
    delete(url: string): Promise<boolean>;
    keys(): Promise<readonly { url: string }[]>;
}
export interface StorageLike {
    getItem(key: string): string | null;
    setItem(key: string, value: string): void;
    removeItem(key: string): void;
}

export type Digest = (bytes: Uint8Array) => Promise<string>;

/** sha-256 of `bytes` as lower-case hex, with WebCrypto. */
export const sha256Hex: Digest = async (bytes) => {
    const hash = await crypto.subtle.digest("SHA-256", bytes as BufferSource);
    return [...new Uint8Array(hash)].map((b) => b.toString(16).padStart(2, "0")).join("");
};

export class ChecksumError extends Error {
    constructor(public readonly file: string) {
        super(`${file} did not match its pinned checksum, so it was not used.`);
    }
}

/** True when every file of the source is in the cache (nothing is read). */
export async function isCached(caches: CachesLike, source: Pick<ModelSource, "files">): Promise<boolean> {
    const cache = await caches.open(MODEL_CACHE);
    for (const f of source.files) {
        if (!(await cache.match(f.url))) return false;
    }
    return true;
}

export interface GetFileOptions {
    caches: CachesLike;
    fetch: (url: string, init?: RequestInit) => Promise<Response>;
    digest?: Digest;
    signal?: AbortSignal;
    /** Bytes of this file read so far (from the network; a cache hit reports its size once). */
    onProgress?: (loaded: number) => void;
}

/**
 * The file's bytes: from the cache, or downloaded, checked against its sha256 and only then
 * cached. A download that fails, is aborted or does not match is never stored.
 */
export async function getModelFile(file: ResolvedFile, { caches, fetch, digest = sha256Hex, signal, onProgress }: GetFileOptions): Promise<Uint8Array> {
    const cache = await caches.open(MODEL_CACHE);
    const hit = await cache.match(file.url);
    if (hit) {
        const bytes = new Uint8Array(await hit.arrayBuffer());
        onProgress?.(bytes.length);
        return bytes;
    }
    const res = await fetch(file.url, { signal, cache: "no-store" });
    if (!res.ok || !res.body) throw new Error(`Could not download ${file.path} (HTTP ${res.status}).`);
    const reader = res.body.getReader();
    const bytes = new Uint8Array(file.bytes);
    let at = 0;
    for (;;) {
        const { done, value } = await reader.read();
        if (done) break;
        if (at + value.length > bytes.length) throw new ChecksumError(file.path);
        bytes.set(value, at);
        at += value.length;
        onProgress?.(at);
    }
    if (at !== file.bytes || (await digest(bytes)) !== file.sha256) throw new ChecksumError(file.path);
    await cache.put(file.url, new Response(bytes as BlobPart, { headers: { "Content-Type": "application/octet-stream" } }));
    return bytes;
}

/** Deletes every cached file that is not one of `keep` (other revisions, other models). */
export async function pruneModelCache(caches: CachesLike, keep: Pick<ModelSource, "files">): Promise<number> {
    const cache = await caches.open(MODEL_CACHE);
    const wanted = new Set(keep.files.map((f) => f.url));
    let removed = 0;
    for (const req of await cache.keys()) {
        if (!wanted.has(req.url)) {
            await cache.delete(req.url);
            removed++;
        }
    }
    return removed;
}

/** "Remove from this device": the whole cache and the index. */
export async function removeModelCache(caches: CachesLike, storage: StorageLike | null): Promise<void> {
    await caches.delete(MODEL_CACHE);
    storage?.removeItem(MODEL_INDEX_KEY);
}

export interface CachedIndex {
    id: string;
    revision: string;
    bytes: number;
}

export function readCachedIndex(storage: StorageLike | null): CachedIndex | null {
    try {
        const raw = storage?.getItem(MODEL_INDEX_KEY);
        if (!raw) return null;
        const v = JSON.parse(raw) as Partial<CachedIndex>;
        return typeof v.id === "string" && typeof v.revision === "string" && typeof v.bytes === "number" ? (v as CachedIndex) : null;
    } catch {
        return null;
    }
}

export function writeCachedIndex(storage: StorageLike | null, source: Pick<ModelSource, "id" | "revision" | "bytes">): void {
    try {
        storage?.setItem(MODEL_INDEX_KEY, JSON.stringify({ id: source.id, revision: source.revision, bytes: source.bytes }));
    } catch {
        // A full or blocked localStorage only loses the size shown on the Me page.
    }
}

/** Is there room for the download? `navigator.storage.estimate()` must show twice its size free. */
export function hasRoomFor(bytes: number, estimate: { quota?: number; usage?: number } | null): boolean {
    if (!estimate?.quota) return true;
    return estimate.quota - (estimate.usage ?? 0) >= 2 * bytes;
}
