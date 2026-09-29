// One candidate's extract(note) and the scoring of a run, shared by run.mjs (Node) and the
// browser harness's worker.
import { createGliner } from "./gliner.mjs";
import { distinct, mask, tidy } from "./text.mjs";

export const LABELS = ["character", "place", "faction", "item", "event"];
const KIND = { character: "Character", place: "Place", faction: "Faction", item: "Item", event: "Event" };
/** GLiNER keeps every span down to this; scoring sweeps thresholds above it. */
export const FLOOR = 0.2;

/** `extract(text)` → every span with its kind and confidence, before any threshold. */
export function makeExtractor({ ort, session, tokenizer, config }) {
    const gliner = createGliner({ ort, session, tokenizer, config });
    return async (text) => (await gliner.extract(mask(text), LABELS, FLOOR)).map((s) => ({ ...s, kind: KIND[s.kind] }));
}

/**
 * Scores a run at `threshold`: P/R/F1 over every tidied span (each occurrence counts), and
 * top-3 over the distinct spans a note would show. `scoreNotes` is web's `score.ts`.
 */
export function scoreRun(scoreNotes, notes, perNote, threshold, prefer = "longest") {
    const byId = new Map(perNote.map((p) => [p.id, p.spans]));
    const scored = [];
    const shown = [];
    for (const n of notes.filter((x) => byId.has(x.id))) {
        const raw = byId.get(n.id).filter((s) => s.confidence >= threshold);
        const spans = tidy(n.text, raw, prefer);
        scored.push({ text: n.text, gold: n.gold, predicted: spans });
        shown.push({ text: n.text, gold: n.gold, predicted: distinct(spans) });
    }
    return { threshold, ...scoreNotes(scored), top3: scoreNotes(shown).top3 };
}

export const percentile = (xs, p) => {
    const s = [...xs].sort((a, b) => a - b);
    return s[Math.min(s.length - 1, Math.ceil((p / 100) * s.length) - 1)];
};

export function latencyBySize(perNote) {
    const out = {};
    for (const size of ["short", "typical", "long"]) {
        const xs = perNote.filter((p) => p.size === size).map((p) => p.ms);
        if (xs.length) out[size] = { n: xs.length, p50: percentile(xs, 50), p95: percentile(xs, 95) };
    }
    return out;
}
