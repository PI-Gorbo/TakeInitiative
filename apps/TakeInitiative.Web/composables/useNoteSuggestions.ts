// Step 23e: ✨ suggestions on the author's own note cards in the stream. Lazy and capped:
// - only the viewer's own notes (only the author can accept, invariant 4);
// - only while the model is `ready` in this tab. The stream never downloads it: it only loads a
//   model already on this device, and only where the device setting allows that without a tap
//   (23b's `decideLoad`: never Off, never on mobile data);
// - only notes in view (one IntersectionObserver for the tab), a note at a time in the worker,
//   when the main thread is idle; a note scrolled away before its turn is skipped, and at most
//   `INLINE_QUEUE_MAX` wait. Spans are matched in batches of up to 50 once the queue drains.
// Results live in memory for the tab only. Nothing is linked or created without a tap.

import type { SessionNote } from "~/utils/api/types";
import type { ModelSpan } from "~/utils/extraction/spans";
import { matchSuggestionSpans } from "~/utils/queries/suggestions";
import {
    dismissKey,
    inlineSuggestions,
    isLinkableSpan,
    MATCH_BATCH_SIZE,
    readDismissed,
    toModelSuggestions,
    withDismissed,
    withQueued,
    writeDismissed,
    type ModelSuggestion,
    type SuggestionModel,
} from "~/utils/suggestions";

type Registered = { campaignId: string; text: string };
type Read = { text: string; suggestions: ModelSuggestion[] };
type Pending = {
    noteId: string;
    campaignId: string;
    text: string;
    spans: ModelSpan[];
};

// Tab-wide, like the extractor.
const results = shallowRef(new Map<string, Read>());
const dismissed = ref<string[]>([]);
const registered = new Map<string, Registered>();
const inView = new Set<string>();
const targets = new WeakMap<Element, string>();
let queue: string[] = [];
let pumping = false;
let observer: IntersectionObserver | null = null;
let triedLoad = false;
let started = false;
let ctx: {
    extractor: ReturnType<typeof useExtractor>;
    api: ReturnType<typeof useApi>;
} | null = null;

const storage = () =>
    typeof localStorage === "undefined" ? null : localStorage;

/** Waits for the main thread to be idle (the worker does the work; this only spaces requests). */
const idle = () =>
    new Promise<void>((resolve) =>
        typeof requestIdleCallback === "function"
            ? requestIdleCallback(() => resolve(), { timeout: 2000 })
            : setTimeout(resolve, 50)
    );

const isFresh = (noteId: string) => {
    const note = registered.get(noteId);
    return !!note && results.value.get(noteId)?.text === note.text;
};

function request(noteId: string) {
    if (!registered.has(noteId) || !inView.has(noteId) || isFresh(noteId))
        return;
    queue = withQueued(queue, noteId);
    void pump();
}

async function flush(pending: Pending[]) {
    if (!ctx || pending.length === 0) return;
    const byCampaign = new Map<string, Pending[]>();
    for (const p of pending)
        byCampaign.set(p.campaignId, [
            ...(byCampaign.get(p.campaignId) ?? []),
            p,
        ]);
    for (const [campaignId, list] of byCampaign) {
        const matches = await matchSuggestionSpans(
            ctx.api,
            campaignId,
            list.flatMap((p) =>
                p.spans.map((s) => ({ text: s.text, kind: s.kind }))
            )
        );
        const next = new Map(results.value);
        let at = 0;
        for (const p of list) {
            next.set(p.noteId, {
                text: p.text,
                suggestions: toModelSuggestions(
                    p.noteId,
                    p.spans,
                    matches.slice(at, at + p.spans.length)
                ),
            });
            at += p.spans.length;
        }
        results.value = next;
    }
}

async function pump() {
    if (pumping || !ctx) return;
    pumping = true;
    const { extractor } = ctx;
    let pending: Pending[] = [];
    try {
        while (queue.length && extractor.state.value === "ready") {
            // The note that came into view last goes first.
            const noteId = queue.pop()!;
            const note = registered.get(noteId);
            if (!note || !inView.has(noteId) || isFresh(noteId)) continue;
            await idle();
            const spans = (await extractor.extract(noteId, note.text)).filter(
                (s) => isLinkableSpan(s.text)
            );
            pending.push({
                noteId,
                campaignId: note.campaignId,
                text: note.text,
                spans,
            });
            const spanCount = pending.reduce((n, p) => n + p.spans.length, 0);
            if (queue.length === 0 || spanCount >= MATCH_BATCH_SIZE) {
                const batch = pending;
                pending = [];
                await flush(batch);
            }
        }
        await flush(pending);
    } catch {
        // The model stopped or `match` failed: the notes are read again when they come back into view.
    } finally {
        pumping = false;
    }
}

function start() {
    if (started || !import.meta.client) return;
    started = true;
    dismissed.value = readDismissed(storage());
    observer = new IntersectionObserver(
        (entries) => {
            for (const e of entries) {
                const noteId = targets.get(e.target);
                if (!noteId) continue;
                if (e.isIntersecting) {
                    inView.add(noteId);
                    request(noteId);
                } else {
                    inView.delete(noteId);
                }
            }
        },
        { rootMargin: "200px 0px" }
    );
    // When the model becomes ready (here, on the loose-ends page or the Me page), read what is in view.
    effectScope(true).run(() =>
        watch(
            () => ctx?.extractor.state.value,
            (state) => {
                if (state === "ready") for (const id of inView) request(id);
                else queue = [];
            }
        )
    );
}

/**
 * One stream card's ✨ suggestions. `enabled` is true only on the viewer's own, posted note in
 * the stream; `el` is the card, observed while enabled.
 */
export function useNoteSuggestions(options: {
    campaignId: () => string;
    note: () => Pick<SessionNote, "id" | "text">;
    el: Readonly<Ref<HTMLElement | null>>;
    enabled: () => boolean;
}) {
    const extractor = useExtractor();
    if (!ctx && import.meta.client) ctx = { extractor, api: useApi() };
    start();
    const model: SuggestionModel = {
        name: extractor.model.value.name,
        version: extractor.model.value.version,
    };

    watch(
        [
            () => options.el.value,
            () => options.enabled(),
            () => options.note().id,
        ],
        ([el, on, noteId], _old, onCleanup) => {
            if (!el || !on || !observer || !options.note().text) return;
            registered.set(noteId, {
                campaignId: options.campaignId(),
                text: options.note().text,
            });
            targets.set(el, noteId);
            observer.observe(el);
            // Load a model already on this device, once per tab, where the setting allows it
            // without a tap. Never a download from the stream.
            if (
                !triedLoad &&
                extractor.state.value === "idle" &&
                extractor.cached.value
            ) {
                triedLoad = true;
                void extractor.ensure();
            }
            onCleanup(() => {
                observer?.unobserve(el);
                targets.delete(el);
                inView.delete(noteId);
                registered.delete(noteId);
            });
        },
        { immediate: true }
    );

    // An edit (or an accepted suggestion) changes the text: read it again if it is in view.
    watch(
        () => options.note().text,
        (text) => {
            const id = options.note().id;
            const note = registered.get(id);
            if (!note) return;
            note.text = text;
            request(id);
        }
    );

    const dismissedSet = computed(() => new Set(dismissed.value));
    const suggestions = computed<ModelSuggestion[]>(() => {
        if (!options.enabled() || extractor.state.value !== "ready") return [];
        const note = options.note();
        const read = results.value.get(note.id);
        if (!read) return [];
        return inlineSuggestions(note.text, read.suggestions, (s) =>
            dismissedSet.value.has(dismissKey(s.noteId, s.text, model.version))
        );
    });

    /** ✕: hide a suggestion on this device (the same list as the loose-ends page). */
    function dismiss(s: ModelSuggestion) {
        dismissed.value = withDismissed(
            dismissed.value,
            dismissKey(s.noteId, s.text, model.version)
        );
        writeDismissed(storage(), dismissed.value);
    }

    return { suggestions, dismiss, model };
}
