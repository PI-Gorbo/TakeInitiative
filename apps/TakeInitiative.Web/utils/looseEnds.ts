// Loose ends in the Wiki and on sessions (19e, design §5). The API lists and counts
// them per viewer (19b); these are the web's pure rules: turning a link suggestion
// into a mention, the rows' labels, the page's URL, and keeping a resolved row on
// screen for a moment.
import type { LinkSuggestion, LooseEnd, LooseEndCounts, LooseEndKind } from "./api/types";
import { NOTE_LINK_PARAM } from "./noteActions";

// ── Linking a span ───────────────────────────────────────────────────────────

/** `@[text](entry:id)`, the stored form of a mention (15d). */
const STORED_MENTION = /@\[(?:[^[\]]|\[[^[\]]*\])*\]\(entry:[^)\s]*\)/g;

/** The `[start, end)` ranges of the stored mentions in `text`. */
function mentionRanges(text: string): [number, number][] {
    return [...text.matchAll(STORED_MENTION)].map((m) => [m.index!, m.index! + m[0].length]);
}

const overlaps = (start: number, end: number, ranges: readonly [number, number][]) =>
    ranges.some(([a, b]) => start < b && end > a);

/** Whether the characters either side of `[start, end)` end a word, so "Gund" never links inside "Gundren". */
const wordBoundaries = (text: string, start: number, end: number) =>
    !/[\p{L}\p{M}\p{Nd}]/u.test(text.slice(Math.max(0, start - 1), start)) &&
    !/[\p{L}\p{M}\p{Nd}]/u.test(text.slice(end, end + 1));

/**
 * Where `span` now sits in `text`: at `start` when it is still there, else the nearest
 * whole-word copy of it that is not inside a mention (the note was edited since the
 * suggestion was made). Null when there is none. Offsets are UTF-16, as the API's.
 */
export function findSpan(text: string, span: Pick<LinkSuggestion, "start" | "text">): number | null {
    if (!span.text) return null;
    const ranges = mentionRanges(text);
    const fits = (at: number) =>
        text.startsWith(span.text, at) &&
        !overlaps(at, at + span.text.length, ranges) &&
        wordBoundaries(text, at, at + span.text.length);
    if (fits(span.start)) return span.start;
    let best: number | null = null;
    for (let at = text.indexOf(span.text); at !== -1; at = text.indexOf(span.text, at + 1)) {
        if (fits(at) && (best === null || Math.abs(at - span.start) < Math.abs(best - span.start))) best = at;
    }
    return best;
}

/**
 * The note's text with the suggested span turned into a mention of the entry, keeping
 * the words the author wrote (invariant 6): "we met gundren" → "we met
 * @[gundren](entry:id)". Null when the span is no longer in the text.
 */
export function linkSpan(text: string, span: Pick<LinkSuggestion, "start" | "text">, entryId: string): string | null {
    const at = findSpan(text, span);
    if (at === null) return null;
    const end = at + span.text.length;
    return `${text.slice(0, at)}@[${span.text}](entry:${entryId})${text.slice(end)}`;
}

// ── Rows ─────────────────────────────────────────────────────────────────────

/** A row's identity: an entry that is both `Other` and empty is two rows. */
export const looseEndKey = (item: Pick<LooseEnd, "kind" | "note" | "entry">) =>
    `${item.kind}:${item.note?.id ?? item.entry?.id ?? ""}`;

export const LOOSE_END_KIND_LABELS: Record<LooseEndKind, string> = {
    UnlinkedNote: "Unlinked note",
    UntaggedImageNote: "Untagged image",
    OtherKind: "Kind is Other",
    EmptyArticle: "No summary",
};

export const isNoteLooseEnd = (kind: LooseEndKind) => kind === "UnlinkedNote" || kind === "UntaggedImageNote";

/** "1 mention", "7 mentions". */
export const mentionCountLabel = (count: number) => `${count} ${count === 1 ? "mention" : "mentions"}`;

/** "1 loose end", "3 loose ends". */
export const looseEndCountLabel = (count: number) => `${count} loose ${count === 1 ? "end" : "ends"}`;

/** A session divider's aria-label: "3 loose ends in Session 12". */
export const dividerLooseEndsLabel = (count: number, sessionNumber: number) =>
    `${looseEndCountLabel(count)} in Session ${sessionNumber}`;

// ── Counts ───────────────────────────────────────────────────────────────────

/** One session's count from `counts.bySession` (keys are GUIDs; case is not trusted). */
export function sessionLooseEndCount(counts: LooseEndCounts | undefined, sessionId: string): number {
    if (!counts) return 0;
    const direct = counts.bySession[sessionId];
    if (direct !== undefined) return direct;
    const wanted = sessionId.toLowerCase();
    for (const [id, count] of Object.entries(counts.bySession)) if (id.toLowerCase() === wanted) return count;
    return 0;
}

// ── The page's URL ───────────────────────────────────────────────────────────

/** `wiki/loose-ends?session=12`: the page narrowed to one session, by number. */
export const LOOSE_END_SESSION_PARAM = "session";

export function looseEndsHref(campaignId: string, options: { session?: number | null; note?: string | null } = {}) {
    const query: Record<string, string> = {};
    if (options.session != null) query[LOOSE_END_SESSION_PARAM] = String(options.session);
    if (options.note) query[NOTE_LINK_PARAM] = options.note;
    return { path: `/app/campaigns/${encodeURIComponent(campaignId)}/wiki/loose-ends`, query };
}

/** `?session=12` as a session number, else null. */
export function sessionFromQuery(value: unknown): number | null {
    const raw = Array.isArray(value) ? value[0] : value;
    if (typeof raw !== "string" || !/^\d+$/.test(raw)) return null;
    const number = Number(raw);
    return number > 0 ? number : null;
}

/** The items of one session (by number), or all of them for null. */
export const itemsInSession = (items: readonly LooseEnd[], session: number | null) =>
    session === null ? [...items] : items.filter((i) => i.sessionNumber === session);

/** The page's heading: "Loose ends (7)", or "Session 12 · 3 loose ends". */
export const looseEndsHeading = (count: number, session: number | null) =>
    session === null ? `Loose ends (${count})` : `Session ${session} · ${looseEndCountLabel(count)}`;

// ── A resolved row stays for a moment ────────────────────────────────────────

/** A row that was just resolved, kept where it was until it has shown its ✓. */
export type HeldLooseEnd = { item: LooseEnd; index: number };

/**
 * The rows to draw: the list as the API has it, with each held row put back at its
 * old place if the refetch already dropped it, so it can show ✓ before it leaves.
 */
export function withHeld(items: readonly LooseEnd[], held: readonly HeldLooseEnd[]): LooseEnd[] {
    const out = [...items];
    const present = new Set(items.map(looseEndKey));
    for (const h of [...held].sort((a, b) => a.index - b.index)) {
        if (present.has(looseEndKey(h.item))) continue;
        out.splice(Math.min(h.index, out.length), 0, h.item);
    }
    return out;
}
