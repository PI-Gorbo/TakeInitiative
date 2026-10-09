// Step 23e: ✨ suggestions on the author's own note cards in the stream. Lazy and capped:
// - only the viewer's own notes (only the author can accept, invariant 4);
// - only while the model is `ready` in this tab. The stream never downloads it: it only loads a
//   model already on this device, and only where the device setting allows that without a tap
//   (23b's `decideLoad`: never Off, never on mobile data);
// - only notes in view (one IntersectionObserver for the tab), a note at a time in the worker,
//   when the main thread is idle; a note scrolled away before its turn is skipped, and at most
//   `INLINE_QUEUE_MAX` wait. Spans are matched in batches of up to 50 once the queue drains.
// Results live in memory for the tab only. Nothing is linked or created without a tap.
//
// Step 23f: and "✨ Find suggestions" in a note's own menu, which reads that **one** note on
// demand. `ask()` reads it now whether or not the stream would have (it also loads the model on
// the tap, which the automatic path never does), and undoes ✕ on this device, because the author
// has just asked to see everything. `lookAgain()` goes a level deeper — `passAt`, 23f — dropping
// the confidence threshold and raising the span cap, so spans the pass before it threw away come
// through, marked unsure. The depth is per note and lasts for the tab. Deeper is only offered on
// one note at a time: the same threshold over a whole page would be mostly noise.

import type { SessionNote } from "~/utils/api/types";
import {
    passAt,
    SUGGESTION_DEPTH_MAX,
    type ModelSpan,
} from "~/utils/extraction/spans";
import { matchSuggestionSpans } from "~/utils/queries/suggestions";
import {
    dismissKey,
    inlineSuggestions,
    isLinkableSpan,
    MATCH_BATCH_SIZE,
    MAX_MODEL_SUGGESTIONS_PER_NOTE,
    readDismissed,
    toModelSuggestions,
    withDismissed,
    withoutNoteDismissals,
    withQueued,
    writeDismissed,
    type ModelSuggestion,
    type SuggestionModel,
} from "~/utils/suggestions";

type Registered = { campaignId: string; text: string };
type Read = { text: string; level: number; suggestions: ModelSuggestion[] };
type Pending = {
    noteId: string;
    campaignId: string;
    text: string;
    level: number;
    spans: ModelSpan[];
};

/** Where one note's explicit ask has got to (23f). The automatic reads stay `idle`. */
export type NoteAskPhase = "idle" | "working" | "done" | "error";

// Tab-wide, like the extractor.
const results = shallowRef(new Map<string, Read>());
/** The pass each note has been asked for (23f); absent means the automatic level 0. */
const levels = shallowRef(new Map<string, number>());
const phases = shallowRef(new Map<string, NoteAskPhase>());
const dismissed = ref<string[]>([]);
const registered = new Map<string, Registered>();
const inView = new Set<string>();
/** Notes the author explicitly asked about: these are read even when scrolled out of view. */
const asked = new Set<string>();
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

const levelOf = (noteId: string) => levels.value.get(noteId) ?? 0;

function setLevel(noteId: string, level: number) {
    const next = new Map(levels.value);
    next.set(noteId, level);
    levels.value = next;
}

function setPhase(noteId: string, phase: NoteAskPhase) {
    if ((phases.value.get(noteId) ?? "idle") === phase) return;
    const next = new Map(phases.value);
    next.set(noteId, phase);
    phases.value = next;
}

/** The displayed cap: three chips on the automatic pass (23d.3), the pass's own once asked. */
const capFor = (level: number, base: number) =>
    level === 0 ? MAX_MODEL_SUGGESTIONS_PER_NOTE : passAt(level, base).max;

/** Read at the depth this note is currently asked for, against its current text. */
const isFresh = (noteId: string) => {
    const note = registered.get(noteId);
    const read = results.value.get(noteId);
    return (
        !!note &&
        !!read &&
        read.text === note.text &&
        read.level === levelOf(noteId)
    );
};

/** `force` is the author's own ask: it skips the in-view rule and reports a phase. */
function request(noteId: string, force = false) {
    if (!registered.has(noteId)) return;
    if (!force && !inView.has(noteId)) return;
    if (isFresh(noteId)) {
        if (force) setPhase(noteId, "done");
        return;
    }
    if (force) setPhase(noteId, "working");
    // Appended last, and `pump` takes the last: an explicit ask jumps the queue.
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
                level: p.level,
                suggestions: toModelSuggestions(
                    p.noteId,
                    p.spans,
                    matches.slice(at, at + p.spans.length)
                ),
            });
            at += p.spans.length;
        }
        results.value = next;
        for (const p of list) setPhase(p.noteId, "done");
    }
}

async function pump() {
    if (pumping || !ctx) return;
    pumping = true;
    const { extractor } = ctx;
    let pending: Pending[] = [];
    // The batch in `match` right now: a failure there is reported against its notes too.
    let flushing: Pending[] = [];
    // The note in the worker right now, so a failure is reported against it.
    let current: string | null = null;
    try {
        while (queue.length && extractor.state.value === "ready") {
            // The note that came into view last goes first.
            const noteId = (current = queue.pop()!);
            const note = registered.get(noteId);
            if (
                !note ||
                (!asked.has(noteId) && !inView.has(noteId)) ||
                isFresh(noteId)
            ) {
                current = null;
                continue;
            }
            await idle();
            const level = levelOf(noteId);
            const pass = passAt(level, extractor.source.value.threshold);
            const spans = (
                await extractor.extract(noteId, note.text, pass)
            ).filter((s) => isLinkableSpan(s.text));
            pending.push({
                noteId,
                campaignId: note.campaignId,
                text: note.text,
                level,
                spans,
            });
            current = null;
            const spanCount = pending.reduce((n, p) => n + p.spans.length, 0);
            if (queue.length === 0 || spanCount >= MATCH_BATCH_SIZE) {
                flushing = pending;
                pending = [];
                await flush(flushing);
                flushing = [];
            }
        }
        await flush(pending);
    } catch {
        // The model stopped or `match` failed: the notes are read again when they come back into
        // view. A note the author asked about says so instead, so the sheet can offer Retry.
        if (current) setPhase(current, "error");
        for (const p of [...flushing, ...pending]) setPhase(p.noteId, "error");
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
    // When the model becomes ready (here, on the loose-ends page or the Me page), read what is in
    // view, and whatever the author asked for while it was still loading.
    effectScope(true).run(() =>
        watch(
            () => ctx?.extractor.state.value,
            (state) => {
                if (state !== "ready") {
                    queue = [];
                    return;
                }
                for (const id of inView) request(id);
                for (const id of asked) request(id, true);
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
    /** The model's pinned threshold: under it a span is only here because the author asked again. */
    const baseThreshold = computed(() => extractor.source.value.threshold);

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

    // An edit (or an accepted suggestion) changes the text: read it again if it is in view, or
    // straight away on a note the author asked about.
    watch(
        () => options.note().text,
        (text) => {
            const id = options.note().id;
            const note = registered.get(id);
            if (!note) return;
            note.text = text;
            request(id, asked.has(id));
        }
    );

    const dismissedSet = computed(() => new Set(dismissed.value));
    const isDismissed = (s: ModelSuggestion) =>
        dismissedSet.value.has(dismissKey(s.noteId, s.text, model.version));

    const suggestions = computed<ModelSuggestion[]>(() => {
        if (!options.enabled() || extractor.state.value !== "ready") return [];
        const note = options.note();
        const read = results.value.get(note.id);
        if (!read) return [];
        return inlineSuggestions(
            note.text,
            read.suggestions,
            isDismissed,
            capFor(read.level, baseThreshold.value)
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

    // ── Asking again, on this note only (23f) ────────────────────────────────

    /** The pass this note is showing: 0 is the automatic read, higher is an author's "Look again". */
    const level = computed(() => levels.value.get(options.note().id) ?? 0);
    const phase = computed<NoteAskPhase>(
        () => phases.value.get(options.note().id) ?? "idle"
    );
    const canLookAgain = computed(() => level.value < SUGGESTION_DEPTH_MAX);

    /**
     * "✨ Find suggestions" from the note's menu: reads this one note now, at the depth it is
     * already at. ✕ on this note is undone — the author has just asked to see everything.
     * The model must be `ready`; the caller loads it on the tap first.
     */
    function ask() {
        const id = options.note().id;
        dismissed.value = withoutNoteDismissals(dismissed.value, id);
        writeDismissed(storage(), dismissed.value);
        asked.add(id);
        request(id, true);
    }

    /** "Look again": one pass deeper, while there is one left. */
    function lookAgain() {
        const id = options.note().id;
        if (levelOf(id) >= SUGGESTION_DEPTH_MAX) return;
        asked.add(id);
        setLevel(id, levelOf(id) + 1);
        request(id, true);
    }

    return {
        suggestions,
        dismiss,
        model,
        baseThreshold,
        level,
        phase,
        canLookAgain,
        ask,
        lookAgain,
    };
}
