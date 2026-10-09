// Text helpers shared by the Node runner and the browser harness. A sketch of 23b's
// `utils/extraction/spans.ts`: masking before inference and clean-up after. Offsets
// are UTF-16 throughout.

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

export const fold = (s) => s.toLowerCase().normalize("NFD").replace(/\p{Mn}/gu, "").normalize("NFC");

// Mentions, link targets, inline code and bare URLs (as LinkSpans' NotProse).
const NOT_PROSE = /@\[(?:[^[\]]|\[[^[\]]*\])*\]\([^)\s]*\)|\]\([^)\s]*\)|`[^`\n]*`|https?:\/\/\S+/giu;
// Markdown syntax that is never part of a name: headings, list markers, quotes, emphasis, brackets.
const MARKDOWN = /^[ \t]*(?:#{1,6}[ \t]|[-*+][ \t]|\d+[.)][ \t]|>[ \t]?)|\*\*|__|[*_[\]]/gmu;

/** The note with mentions, link targets, code, URLs and markdown syntax replaced by spaces of the same length. */
export function mask(text) {
    const blank = (m) => " ".repeat(m.length);
    return text.replace(NOT_PROSE, blank).replace(MARKDOWN, blank);
}

/**
 * 23b's clean-up of a model's spans: strip a leading "the" and a trailing 's, drop dice,
 * numbers and stop words, and resolve overlaps (`prefer`: "confidence" keeps the more
 * confident, "longest" keeps the longer then the more confident).
 */
export function tidy(text, spans, prefer = "confidence") {
    const out = [];
    for (const s of spans) {
        let start = s.start;
        let end = s.start + s.length;
        let piece = text.slice(start, end);
        const lead = /^(?:the|The|THE)\s+/u.exec(piece);
        if (lead && lead[0].length < piece.length) {
            start += lead[0].length;
        }
        piece = text.slice(start, end);
        const poss = /['’][sS]$/u.exec(piece);
        if (poss && poss.index > 0) {
            end -= poss[0].length;
        }
        piece = text.slice(start, end).trim();
        if (!piece || /^\d*d\d+/iu.test(piece) || /^[\d\s.,:+-]+$/u.test(piece) || STOP_WORDS.has(fold(piece))) {
            continue;
        }
        if (!/\p{L}/u.test(piece)) {
            continue;
        }
        out.push({ ...s, start, length: end - start, text: text.slice(start, end) });
    }
    const order = [...out].sort((a, b) =>
        prefer === "longest" ? b.length - a.length || b.confidence - a.confidence : b.confidence - a.confidence
    );
    const kept = [];
    for (const s of order) {
        if (!kept.some((k) => s.start < k.start + k.length && k.start < s.start + s.length)) {
            kept.push(s);
        }
    }
    return kept.sort((a, b) => a.start - b.start);
}

/** One span per distinct folded text (the most confident), as 23b sends to matching and the UI shows. */
export function distinct(spans) {
    const best = new Map();
    for (const s of spans) {
        const key = fold(s.text);
        if (!best.has(key) || best.get(key).confidence < s.confidence) best.set(key, s);
    }
    return [...best.values()].sort((a, b) => a.start - b.start);
}

/** Sentence ranges [start, end) of `text`, for chunking a long note. */
export function sentences(text) {
    const out = [];
    const re = /[^.!?\n]+(?:[.!?]+|\n+|$)/gu;
    for (const m of text.matchAll(re)) {
        if (m[0].trim()) out.push([m.index, m.index + m[0].length]);
    }
    return out.length ? out : [[0, text.length]];
}
