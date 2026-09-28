// Step 23b: the suggestion model for the rest of the app. One worker per tab, created only when
// `ensure()` is first called and the device setting allows it, and kept for the tab's life once
// loaded. Nothing loads with the app. Results are memoised per (note, text, model version) in
// memory only. Suggestions only: nothing here creates an entry or a mention.

import { resolveModelSource, type ModelSource, type SuggestionsConfig } from "~/utils/extraction/modelSource";
import { hasRoomFor, isCached, pruneModelCache, readCachedIndex, removeModelCache, writeCachedIndex, type CachedIndex } from "~/utils/extraction/modelCache";
import { decideLoad, isMetered, readSuggestionsSetting, writeSuggestionsSetting, type ConnectionLike, type SuggestionsSetting } from "~/utils/extraction/deviceSetting";
import type { FromWorker, ToWorker } from "~/utils/extraction/messages";
import type { ModelSpan } from "~/utils/extraction/spans";

export type ExtractorState = "off" | "idle" | "needsConsent" | "downloading" | "loading" | "ready" | "error";

// Tab-wide state, shared by every caller.
const state = ref<ExtractorState>("idle");
const setting = ref<SuggestionsSetting>("ask");
const progress = ref({ loaded: 0, total: 0 });
const error = ref<string | null>(null);
const cached = ref<CachedIndex | null>(null);
let initialised = false;
let worker: Worker | null = null;
let loading: Promise<boolean> | null = null;
let settle: ((ok: boolean) => void) | null = null;
let nextId = 1;
const waiting = new Map<number, { resolve: (s: ModelSpan[]) => void; reject: (e: Error) => void }>();
const memo = new Map<string, ModelSpan[]>();
// One note at a time: `running` is in the worker; `queue` holds the rest by note id, so a newer
// request for the same note replaces the older one (both callers get the newer result).
const queue = new Map<string, { text: string; resolvers: ((s: ModelSpan[] | Error) => void)[] }>();
let running = false;

const storage = () => (typeof localStorage === "undefined" ? null : localStorage);

/** FNV-1a, for the memo key: the text itself is not kept. */
function hash(text: string): string {
    let h = 0x811c9dc5;
    for (let i = 0; i < text.length; i++) {
        h ^= text.charCodeAt(i);
        h = Math.imul(h, 0x01000193);
    }
    return (h >>> 0).toString(36) + text.length.toString(36);
}

export function useExtractor() {
    const config = useRuntimeConfig().public.suggestions as SuggestionsConfig;
    const device = useDevice();
    const source = computed<ModelSource>(() => resolveModelSource(config, typeof location === "undefined" ? "http://localhost" : location.origin));
    const model = computed(() => ({ name: config.id, version: config.version }));

    if (!initialised && import.meta.client) {
        initialised = true;
        setting.value = readSuggestionsSetting(storage());
        cached.value = currentIndex();
        state.value = setting.value === "off" ? "off" : "idle";
    }

    function currentIndex(): CachedIndex | null {
        const i = readCachedIndex(storage());
        return i && i.id === config.id && i.revision === config.revision ? i : null;
    }

    const metered = () => isMetered((navigator as Navigator & { connection?: ConnectionLike }).connection, device.isMobile);

    function onMessage({ data }: MessageEvent<FromWorker>) {
        switch (data.type) {
            case "progress":
                progress.value = { loaded: data.loaded, total: data.total };
                if (!data.fromCache) state.value = "downloading";
                if (data.loaded >= data.total) state.value = "loading";
                break;
            case "ready":
                state.value = "ready";
                writeCachedIndex(storage(), source.value);
                cached.value = currentIndex();
                // Keep only this revision's files; ask to keep them (a refusal is fine).
                pruneModelCache(caches, source.value).catch(() => {});
                navigator.storage?.persist?.().catch(() => false);
                settle?.(true);
                break;
            case "result":
                waiting.get(data.id)?.resolve(data.spans);
                waiting.delete(data.id);
                break;
            case "error":
                if (data.id != null) {
                    waiting.get(data.id)?.reject(new Error(data.message));
                    waiting.delete(data.id);
                    break;
                }
                error.value = data.message;
                state.value = "error";
                stopWorker();
                settle?.(false);
                break;
        }
    }

    function stopWorker() {
        worker?.terminate();
        worker = null;
        loading = null;
        for (const w of waiting.values()) w.reject(new Error("The suggestion model stopped."));
        waiting.clear();
    }

    /**
     * Loads the model if the device setting allows it now; `consent` is the member's tap on
     * "Download" or "Find suggestions". Resolves true when ready, false when it may not load
     * (then `state` says why: `off`, `needsConsent`, `error`).
     */
    async function ensure({ consent = false }: { consent?: boolean } = {}): Promise<boolean> {
        if (!import.meta.client) return false;
        if (state.value === "ready") return true;
        if (loading) return loading;
        error.value = null;
        const isInCache = await isCached(caches, source.value).catch(() => false);
        const decision = decideLoad(setting.value, { cached: isInCache, metered: metered(), consent });
        if (decision !== "load") {
            state.value = decision;
            return false;
        }
        if (!isInCache) {
            const estimate = await navigator.storage?.estimate?.().catch(() => null);
            if (!hasRoomFor(source.value.bytes, estimate ?? null)) {
                error.value = "Not enough storage on this device.";
                state.value = "error";
                return false;
            }
        }
        state.value = isInCache ? "loading" : "downloading";
        progress.value = { loaded: 0, total: source.value.bytes };
        loading = new Promise<boolean>((resolve) => {
            settle = (ok) => {
                settle = null;
                if (!ok) loading = null;
                resolve(ok);
            };
        });
        worker = new Worker(new URL("../workers/extractor.worker.ts", import.meta.url), { type: "module" });
        worker.onmessage = onMessage;
        worker.onerror = (e) => {
            error.value = e.message || "The suggestion model could not start.";
            state.value = "error";
            stopWorker();
            settle?.(false);
        };
        worker.postMessage({ type: "load", source: JSON.parse(JSON.stringify(source.value)) } satisfies ToWorker);
        return loading;
    }

    /** Stops a download in progress. A partial file is never cached. */
    function cancel() {
        if (state.value !== "downloading" && state.value !== "loading") return;
        stopWorker();
        settle?.(false);
        state.value = setting.value === "off" ? "off" : "idle";
    }

    function send(text: string): Promise<ModelSpan[]> {
        return new Promise((resolve, reject) => {
            const id = nextId++;
            waiting.set(id, { resolve, reject });
            worker!.postMessage({ type: "extract", id, text } satisfies ToWorker);
        });
    }

    async function pump() {
        if (running) return;
        running = true;
        try {
            while (queue.size && worker && state.value === "ready") {
                const [noteId, job] = queue.entries().next().value!;
                queue.delete(noteId);
                const result = await send(job.text).catch((e: Error) => e);
                if (!(result instanceof Error)) memo.set(`${noteId}:${hash(job.text)}:${config.version}`, result);
                for (const r of job.resolvers) r(result);
            }
        } finally {
            running = false;
        }
    }

    /** The note's spans. Needs `state === "ready"` (call `ensure()` first). */
    async function extract(noteId: string, text: string): Promise<ModelSpan[]> {
        const key = `${noteId}:${hash(text)}:${config.version}`;
        const hit = memo.get(key);
        if (hit) return hit;
        if (state.value !== "ready" || !worker) throw new Error("The suggestion model is not loaded.");
        return new Promise((resolve, reject) => {
            const had = queue.get(noteId);
            const resolver = (r: ModelSpan[] | Error) => (r instanceof Error ? reject(r) : resolve(r));
            queue.set(noteId, { text, resolvers: [...(had?.resolvers ?? []), resolver] });
            void pump();
        });
    }

    /** "Remove from this device": stops the worker and deletes the cached files. */
    async function remove() {
        stopWorker();
        settle?.(false);
        memo.clear();
        await removeModelCache(caches, storage()).catch(() => {});
        cached.value = null;
        state.value = setting.value === "off" ? "off" : "idle";
    }

    function setSetting(value: SuggestionsSetting) {
        setting.value = value;
        writeSuggestionsSetting(storage(), value);
        if (value === "off") {
            stopWorker();
            settle?.(false);
            state.value = "off";
        } else if (state.value === "off" || state.value === "needsConsent") {
            state.value = "idle";
        }
    }

    return {
        state: readonly(state),
        progress: readonly(progress),
        error: readonly(error),
        setting: readonly(setting),
        cached: readonly(cached),
        source,
        model,
        isMetered: metered,
        ensure,
        cancel,
        extract,
        remove,
        setSetting,
    };
}
