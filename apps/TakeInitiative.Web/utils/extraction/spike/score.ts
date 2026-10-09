/**
 * Step 23a's scorer: how well a candidate model's spans match the invented test set's gold
 * spans. Pure, so the Node spike (scripts/extraction-spike) and a browser harness share it, and
 * 23b keeps it for regression checks. Offsets are UTF-16, as the web indexes strings.
 *
 * - **Exact**: same start and length.
 * - **Overlap**: the spans overlap and differ by at most one word at each end ("the Ember
 *   Court" for "Ember Court", "Rellan's" for "Rellan").
 * - **Kind accuracy**: of the overlap matches, how many have the gold kind.
 * - **Top-3 precision**: the UI shows at most three suggestions a note (19's cap), so per note
 *   the three most confident spans are taken and a span counts when it overlap-matches a gold
 *   span. Summed over every note that shows at least one; `null` when no note shows any.
 *
 * Matching is one-to-one: a gold span is claimed by at most one predicted span, the more
 * confident first.
 */

export interface GoldSpan {
    start: number;
    length: number;
    kind: string;
    text?: string;
}

export interface PredictedSpan extends GoldSpan {
    confidence: number;
}

export interface ScoredNote {
    text: string;
    gold: GoldSpan[];
    predicted: PredictedSpan[];
}

export interface Prf {
    tp: number;
    predicted: number;
    gold: number;
    precision: number;
    recall: number;
    f1: number;
}

export interface Score {
    exact: Prf;
    overlap: Prf;
    /** Matched (overlap) spans whose kind is the gold kind, over all overlap matches; `null` with none. */
    kindAccuracy: number | null;
    top3: { correct: number; shown: number; precision: number | null };
}

export type MatchMode = "exact" | "overlap";

const WORD = /[\p{L}\p{N}][\p{L}\p{M}\p{N}'’-]*/gu;

function wordsIn(text: string): number {
    return text.match(WORD)?.length ?? 0;
}

/** Whether `a` matches gold span `b` in `text` under `mode`. */
export function spansMatch(text: string, a: GoldSpan, b: GoldSpan, mode: MatchMode): boolean {
    const aEnd = a.start + a.length;
    const bEnd = b.start + b.length;
    if (a.start === b.start && aEnd === bEnd) {
        return true;
    }
    if (mode === "exact" || a.start >= bEnd || b.start >= aEnd) {
        return false;
    }
    const head = text.slice(Math.min(a.start, b.start), Math.max(a.start, b.start));
    const tail = text.slice(Math.min(aEnd, bEnd), Math.max(aEnd, bEnd));
    return wordsIn(head) <= 1 && wordsIn(tail) <= 1;
}

/** One-to-one pairs `[predictedIndex, goldIndex]`, the more confident prediction first, an exact match preferred. */
export function matchSpans(text: string, gold: GoldSpan[], predicted: PredictedSpan[], mode: MatchMode): [number, number][] {
    const order = predicted.map((_, i) => i).sort((x, y) => predicted[y]!.confidence - predicted[x]!.confidence);
    const taken = new Set<number>();
    const pairs: [number, number][] = [];
    for (const p of order) {
        const span = predicted[p]!;
        let best = -1;
        for (let g = 0; g < gold.length; g++) {
            if (taken.has(g) || !spansMatch(text, span, gold[g]!, mode)) {
                continue;
            }
            const exact = span.start === gold[g]!.start && span.length === gold[g]!.length;
            if (best === -1 || exact) {
                best = g;
            }
            if (exact) {
                break;
            }
        }
        if (best !== -1) {
            taken.add(best);
            pairs.push([p, best]);
        }
    }
    return pairs;
}

function prf(tp: number, predicted: number, gold: number): Prf {
    // Nothing predicted and nothing to find is perfect; one without the other is not.
    const precision = predicted === 0 ? (gold === 0 ? 1 : 0) : tp / predicted;
    const recall = gold === 0 ? (predicted === 0 ? 1 : 0) : tp / gold;
    const f1 = precision + recall === 0 ? 0 : (2 * precision * recall) / (precision + recall);
    return { tp, predicted, gold, precision, recall, f1 };
}

/** The top `k` spans of a note by confidence. */
export function topSpans(predicted: PredictedSpan[], k = 3): PredictedSpan[] {
    return [...predicted].sort((a, b) => b.confidence - a.confidence).slice(0, k);
}

export function scoreNotes(notes: ScoredNote[], k = 3): Score {
    let exactTp = 0;
    let overlapTp = 0;
    let predicted = 0;
    let gold = 0;
    let kindRight = 0;
    let shown = 0;
    let correct = 0;
    for (const note of notes) {
        predicted += note.predicted.length;
        gold += note.gold.length;
        exactTp += matchSpans(note.text, note.gold, note.predicted, "exact").length;
        const overlap = matchSpans(note.text, note.gold, note.predicted, "overlap");
        overlapTp += overlap.length;
        for (const [p, g] of overlap) {
            if (note.predicted[p]!.kind.toLowerCase() === note.gold[g]!.kind.toLowerCase()) {
                kindRight++;
            }
        }
        const top = topSpans(note.predicted, k);
        shown += top.length;
        correct += matchSpans(note.text, note.gold, top, "overlap").length;
    }
    return {
        exact: prf(exactTp, predicted, gold),
        overlap: prf(overlapTp, predicted, gold),
        kindAccuracy: overlapTp === 0 ? null : kindRight / overlapTp,
        top3: { correct, shown, precision: shown === 0 ? null : correct / shown },
    };
}
