// Step 23b: the text side of extraction, pure and shared by the worker and the tests. Masking
// before inference and the clean-up after it, ported from 23a's `scripts/extraction-spike/lib/text.mjs`.
// Offsets are UTF-16 code units into the note's own text throughout (as 19's link suggestions).

/** The entry kinds the model is asked for, in the order its labels are passed. `Other` is never suggested. */
export const SUGGESTION_KINDS = ["Character", "Place", "Faction", "Item", "Event"] as const;
export type SuggestionKind = (typeof SUGGESTION_KINDS)[number];

/** The model's labels: the kinds in lower case (§11a, no mapping). */
export const LABELS = SUGGESTION_KINDS.map((k) => k.toLowerCase());

/** At most this many spans a note are sent to matching. */
export const MAX_SPANS_PER_NOTE = 10;

/** A span the model found, with offsets into the note. */
export interface ModelSpan {
    start: number;
    length: number;
    text: string;
    kind: SuggestionKind;
    confidence: number;
}

// 19's stop list (LinkSpans.StopWords), compared folded.
export const STOP_WORDS = new Set(
    (
        "a an the and or but so if of to in on at by for as i we he she it they you me us him her them my our his " +
        "its their your this that these those there here then than when where what who whom which while after before later next now also " +
        "with from into onto over under about again back just only very much more most some each every been being have were was will " +
        "would could should might must shall does did done still even yes no not all any both other such too well meanwhile finally " +
        "suddenly today tonight yesterday tomorrow session note notes"
    ).split(" ")
);

export const fold = (s: string) => s.toLowerCase().normalize("NFD").replace(/\p{Mn}/gu, "").normalize("NFC");

// Mentions, link targets, inline code and bare URLs (as LinkSpans' NotProse).
const NOT_PROSE = /@\[(?:[^[\]]|\[[^[\]]*\])*\]\([^)\s]*\)|\]\([^)\s]*\)|`[^`\n]*`|https?:\/\/\S+/giu;
// Markdown syntax that is never part of a name: headings, list markers, quotes, emphasis, brackets.
const MARKDOWN = /^[ \t]*(?:#{1,6}[ \t]|[-*+][ \t]|\d+[.)][ \t]|>[ \t]?)|\*\*|__|[*_[\]]/gmu;

/** The note with mentions, link targets, code, URLs and markdown syntax replaced by spaces of the same length. */
export function mask(text: string): string {
    const blank = (m: string) => " ".repeat(m.length);
    return text.replace(NOT_PROSE, blank).replace(MARKDOWN, blank);
}

/** Sentence ranges [start, end) of `text`, for chunking a long note. */
export function sentences(text: string): [number, number][] {
    const out: [number, number][] = [];
    for (const m of text.matchAll(/[^.!?\n]+(?:[.!?]+|\n+|$)/gu)) {
        if (m[0].trim()) out.push([m.index!, m.index! + m[0].length]);
    }
    return out.length ? out : [[0, text.length]];
}

/**
 * Strips a leading "the" and a trailing 's, drops dice, numbers and stop words, and resolves
 * overlaps: the longest span wins, then the more confident.
 */
export function tidy<T extends { start: number; length: number; confidence: number }>(text: string, spans: T[]): (T & { text: string })[] {
    const out: (T & { text: string })[] = [];
    for (const s of spans) {
        let start = s.start;
        let end = s.start + s.length;
        let piece = text.slice(start, end);
        const lead = /^(?:the|The|THE)\s+/u.exec(piece);
        if (lead && lead[0].length < piece.length) start += lead[0].length;
        piece = text.slice(start, end);
        const poss = /['’][sS]$/u.exec(piece);
        if (poss && poss.index > 0) end -= poss[0].length;
        piece = text.slice(start, end).trim();
        if (!piece || /^\d*d\d+/iu.test(piece) || /^[\d\s.,:+-]+$/u.test(piece) || STOP_WORDS.has(fold(piece))) continue;
        if (!/\p{L}/u.test(piece)) continue;
        out.push({ ...s, start, length: end - start, text: text.slice(start, end) });
    }
    const order = [...out].sort((a, b) => b.length - a.length || b.confidence - a.confidence);
    const kept: (T & { text: string })[] = [];
    for (const s of order) {
        if (!kept.some((k) => s.start < k.start + k.length && k.start < s.start + s.length)) kept.push(s);
    }
    return kept.sort((a, b) => a.start - b.start);
}

/** One span per distinct folded text: the most confident. */
export function distinct<T extends { text: string; start: number; confidence: number }>(spans: T[]): T[] {
    const best = new Map<string, T>();
    for (const s of spans) {
        const key = fold(s.text);
        const had = best.get(key);
        if (!had || had.confidence < s.confidence) best.set(key, s);
    }
    return [...best.values()].sort((a, b) => a.start - b.start);
}

/** A raw span from the model: offsets into the (masked) text and a label index or kind. */
export interface RawSpan {
    start: number;
    length: number;
    kind: SuggestionKind;
    confidence: number;
}

/**
 * The spans a note offers, from the model's raw spans: over the threshold, tidied, one per
 * distinct text, the `max` most confident, in text order.
 */
export function finalSpans(text: string, raw: RawSpan[], threshold: number, max = MAX_SPANS_PER_NOTE): ModelSpan[] {
    const over = raw.filter((s) => s.confidence >= threshold);
    const top = distinct(tidy(text, over))
        .sort((a, b) => b.confidence - a.confidence)
        .slice(0, max);
    return top
        .sort((a, b) => a.start - b.start)
        .map((s) => ({ start: s.start, length: s.length, text: s.text, kind: s.kind, confidence: s.confidence }));
}
