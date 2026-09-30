// Step 23d: model suggestions (glossary §1, design §11a) on the loose-ends page. The model finds
// spans on the device (23b); `POST suggestions/match` says which are existing entries (23c).
// These are the web's pure rules: turning spans and matches into suggestions, merging them with
// 19's link suggestions, the labels, the local dismiss list, the create dialog's defaults, and
// the `PUT notes/{id}` body that accepts one. Nothing here writes anything by itself.
import type {
    EntryKind,
    LinkSuggestion,
    NewEntryRequest,
    SessionNote,
    SuggestionMatch,
    SuggestionRequest,
    Visibility,
} from "./api/types";
import type { StorageLike } from "./extraction/modelCache";
import { fold, mask, type ModelSpan } from "./extraction/spans";
import { findSpan, linkSpan } from "./looseEnds";
import { mentionedEntryIds } from "./markdown";

/** A span the model found in one note, with the entry the matcher found for it (or none). */
export interface ModelSuggestion extends ModelSpan {
    noteId: string;
    /** An existing entry the viewer can see ("→ @Rellan Ashvale?"), or null ("+ Create"). */
    match: SuggestionMatch | null;
}

/** 19's link suggestion, with the model's suggestion of the same entry when there is one (then it shows ✨). */
export type LinkChip = LinkSuggestion & { model: ModelSuggestion | null };

/** What one note row shows: 19's chips (some with ✨), then at most three ✨ chips of its own. */
export interface NoteSuggestions {
    links: LinkChip[];
    model: ModelSuggestion[];
    /** The ✨ count for the row's label: sparkled link chips plus the model's own chips. */
    count: number;
}

/** The model (`name`, provenance `version`) an accepted suggestion records. */
export type SuggestionModel = { name: string; version: string };

/** At most this many ✨ chips of the model's own on a note (23d.3). */
export const MAX_MODEL_SUGGESTIONS_PER_NOTE = 3;
/** `POST suggestions/match` takes at most this many spans a request (23c). */
export const MATCH_BATCH_SIZE = 50;
/** The API's span length limit on `match` (23c). */
export const MATCH_SPAN_MAX = 80;

// ── From spans to suggestions ────────────────────────────────────────────────

/**
 * Whether a span can become a mention as it stands: the API's one-span rule (23c) refuses a
 * span with brackets, a backslash, a backtick, a newline or outer spaces, and `match` one over
 * 80 characters. Such spans are never offered.
 */
export const isLinkableSpan = (text: string) =>
    text.length > 0 &&
    text.length <= MATCH_SPAN_MAX &&
    text === text.trim() &&
    !/[[\]\\`\n\r]/.test(text);

/** `items` in batches of `size` (one `match` request each). */
export function batches<T>(
    items: readonly T[],
    size = MATCH_BATCH_SIZE
): T[][] {
    const out: T[][] = [];
    for (let i = 0; i < items.length; i += size)
        out.push(items.slice(i, i + size));
    return out;
}

/** A note's spans with their matches (`matches[i]` is `spans[i]`'s). */
export const toModelSuggestions = (
    noteId: string,
    spans: readonly ModelSpan[],
    matches: readonly (SuggestionMatch | null)[]
) =>
    spans.map<ModelSuggestion>((span, i) => ({
        ...span,
        noteId,
        match: matches[i] ?? null,
    }));

// ── Merging with 19's link suggestions ───────────────────────────────────────

const overlapsSpan = (
    a: { start: number; length: number },
    b: { start: number; length: number }
) => a.start < b.start + b.length && b.start < a.start + a.length;

/**
 * One note's suggestions (23d.3):
 * - a model match whose entry 19's link suggestions already offer adds ✨ to that chip instead
 *   of a second chip;
 * - a model "create" over a span a link suggestion already covers is dropped (the matcher has
 *   a better answer for those words);
 * - one chip per entry; dismissed ones are left out;
 * - at most three of the model's own chips, matches first, then by confidence.
 */
export function mergeSuggestions(
    links: readonly LinkSuggestion[],
    model: readonly ModelSuggestion[],
    isDismissed: (s: ModelSuggestion) => boolean = () => false,
    max = MAX_MODEL_SUGGESTIONS_PER_NOTE
): NoteSuggestions {
    const visible = model.filter((s) => !isDismissed(s));
    const byConfidence = [...visible].sort(
        (a, b) => b.confidence - a.confidence
    );
    const linkEntries = new Set(links.map((l) => l.entry.id));

    const chips = links.map<LinkChip>((link) => ({
        ...link,
        model:
            byConfidence.find((s) => s.match?.entry.id === link.entry.id) ??
            null,
    }));

    const seenEntries = new Set<string>();
    const own = byConfidence
        .filter((s) => {
            if (s.match) {
                if (
                    linkEntries.has(s.match.entry.id) ||
                    seenEntries.has(s.match.entry.id)
                )
                    return false;
                seenEntries.add(s.match.entry.id);
                return true;
            }
            return !links.some((l) => overlapsSpan(l, s));
        })
        .sort(
            (a, b) =>
                Number(!!b.match) - Number(!!a.match) ||
                b.confidence - a.confidence
        )
        .slice(0, max);

    return {
        links: chips,
        model: own,
        count: chips.filter((c) => c.model).length + own.length,
    };
}

// ── Labels ───────────────────────────────────────────────────────────────────

/** "a Place", "an Item", "an Event". */
export const kindWithArticle = (kind: EntryKind) =>
    `${/^[AEIOU]/.test(kind) ? "an" : "a"} ${kind}`;

/** A ✨ chip's accessible name. */
export const modelSuggestionAriaLabel = (s: ModelSuggestion) =>
    s.match
        ? `Suggestion: link “${s.text}” to ${s.match.entry.name}`
        : `Suggestion: “${s.text}” looks like ${kindWithArticle(s.kind)}. Create it and link it`;

/** "✨ Looking at 12 notes…". */
export const lookingAtLabel = (count: number) =>
    `Looking at ${count} ${count === 1 ? "note" : "notes"}…`;

// ── Dismissing, on this device only ──────────────────────────────────────────

/** `localStorage` key of the dismissed suggestions (23d.6). Nothing is stored on the server. */
export const DISMISSED_KEY = "ti.suggestions.dismissed";
/** At most this many are kept; the oldest go first. */
export const DISMISSED_MAX = 500;

/** Note id + folded span + model version: a new model version may suggest it again. */
export const dismissKey = (noteId: string, text: string, version: string) =>
    `${noteId.toLowerCase()}|${fold(text)}|${version}`;

export function readDismissed(storage: StorageLike | null): string[] {
    try {
        const parsed: unknown = JSON.parse(
            storage?.getItem(DISMISSED_KEY) ?? "[]"
        );
        return Array.isArray(parsed)
            ? parsed
                  .filter((k): k is string => typeof k === "string")
                  .slice(-DISMISSED_MAX)
            : [];
    } catch {
        return [];
    }
}

/** The list with `key` added last (moved there if it was already in), capped at `max`. */
export function withDismissed(
    list: readonly string[],
    key: string,
    max = DISMISSED_MAX
): string[] {
    const out = [...list.filter((k) => k !== key), key];
    return out.length > max ? out.slice(out.length - max) : out;
}

export function writeDismissed(
    storage: StorageLike | null,
    list: readonly string[]
): void {
    try {
        storage?.setItem(DISMISSED_KEY, JSON.stringify(list));
    } catch {
        // Blocked storage: the dismissal lasts for this page only.
    }
}

// ── Creating an entry from a suggestion ──────────────────────────────────────

/**
 * The visibility a new entry created with this note gets: the note's, except that a hidden
 * `Everyone` note gives `DM` (the API's `NewEntries.VisibilityFrom`, which decides it; the
 * dialog only shows it).
 */
export const newEntryVisibility = (
    note: Pick<SessionNote, "visibility" | "isHidden">
): Visibility =>
    note.isHidden && note.visibility === "Everyone" ? "DM" : note.visibility;

/** The create dialog's defaults: the span as the name, the model's kind, the note's visibility. */
export const createDefaults = (
    s: Pick<ModelSuggestion, "text" | "kind">,
    note: Pick<SessionNote, "visibility" | "isHidden">
) => ({
    name: s.text.trim(),
    kind: s.kind as EntryKind,
    visibility: newEntryVisibility(note),
});

// ── Accepting ────────────────────────────────────────────────────────────────

export type AcceptBody = {
    text: string;
    isRecap: boolean;
    suggestion: SuggestionRequest;
    newEntries?: NewEntryRequest[];
};

/**
 * The `PUT notes/{id}` body that accepts a suggestion: the note with exactly the span turned
 * into `@[span](entry:id)` (the author's own words, invariant 6), and the model's provenance at
 * the span's place in the text being replaced (23c's one-span rule). With `newEntry`, the entry
 * is created in the same write, with `entryId` its id. Null when the span is no longer there.
 */
export function acceptBody(
    note: Pick<SessionNote, "text" | "isRecap">,
    s: Pick<ModelSuggestion, "start" | "text" | "confidence">,
    entryId: string,
    model: SuggestionModel,
    newEntry?: Omit<NewEntryRequest, "id">
): AcceptBody | null {
    const at = findSpan(note.text, s);
    const text = linkSpan(note.text, s, entryId);
    if (at === null || text === null) return null;
    return {
        text,
        isRecap: note.isRecap,
        suggestion: {
            model: model.name,
            version: model.version,
            confidence: Math.min(1, Math.max(0, s.confidence)),
            start: at,
            length: s.text.length,
            entryId,
        },
        ...(newEntry
            ? {
                  newEntries: [
                      {
                          id: entryId,
                          name: newEntry.name.trim(),
                          kind: newEntry.kind,
                      },
                  ],
              }
            : {}),
    };
}

// ── Inline on the author's own notes (23e) ───────────────────────────────────

/**
 * One stream note's ✨ chips (23e.1): the model's suggestions merged as on the loose-ends page
 * (no link suggestions there), without a match for an entry the note already mentions, and
 * without a span that is no longer in the note's prose (outside mentions, links and code).
 */
export function inlineSuggestions(
    text: string,
    model: readonly ModelSuggestion[],
    isDismissed: (s: ModelSuggestion) => boolean = () => false
): ModelSuggestion[] {
    const mentioned = new Set(mentionedEntryIds(text).map((id) => id.toLowerCase()));
    // Read from an older text (the author just linked one): keep only spans still in the prose.
    const prose = mask(text);
    return mergeSuggestions(
        [],
        model.filter(
            (s) =>
                prose.includes(s.text) &&
                (!s.match || !mentioned.has(s.match.entry.id.toLowerCase()))
        ),
        isDismissed
    ).model;
}

/** How many own notes in view the stream reads at most, newest in view first (23e.1). */
export const INLINE_QUEUE_MAX = 12;

/** The queue of notes to read, with `noteId` added last, at most `max` (the oldest request goes). */
export function withQueued(
    queue: readonly string[],
    noteId: string,
    max = INLINE_QUEUE_MAX
): string[] {
    const out = [...queue.filter((id) => id !== noteId), noteId];
    return out.length > max ? out.slice(out.length - max) : out;
}

/** "✨ 3" chip's accessible name. */
export const inlineChipAriaLabel = (count: number) =>
    `${count} ${count === 1 ? "suggestion" : "suggestions"} from the suggestion model for this note`;

/** "gliner_small-v2.5 (0.87)": the ✨ line on a note version (23e.3). */
export const suggestedByLabel = (model: {
    name: string;
    confidence: number;
}) =>
    `✨ suggested by ${model.name} (${Math.min(1, Math.max(0, model.confidence)).toFixed(2)})`;

/** "14 mentions in 9 notes", "1 mention in 1 note". */
export const mentionsInNotesLabel = (mentions: number, notes: number) =>
    `${mentions} ${mentions === 1 ? "mention" : "mentions"} in ${notes} ${notes === 1 ? "note" : "notes"}`;

/** Revert's question (23e.4). */
export const revertQuestion = (mentions: number) =>
    `Unlink the ${mentions === 1 ? "mention" : `${mentions} mentions`} this model suggested in your notes? Mentions you typed yourself stay.`;
