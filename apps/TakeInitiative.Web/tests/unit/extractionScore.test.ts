import { describe, expect, it } from "vitest";
import notes from "~/tests/fixtures/extraction/notes.json";
import { matchSpans, scoreNotes, spansMatch, topSpans, type GoldSpan, type PredictedSpan } from "~/utils/extraction/spike/score";

const at = (text: string, span: string, kind: string, confidence = 0.9, from = 0): PredictedSpan => ({
    start: text.indexOf(span, from),
    length: span.length,
    kind,
    confidence,
});

const TEXT = "met rellan at the gates of Greyhollow Keep, and the Ember Court's spies watched.";
const GOLD: GoldSpan[] = [
    { start: 4, length: 6, kind: "Character" },
    { start: TEXT.indexOf("Greyhollow"), length: "Greyhollow Keep".length, kind: "Place" },
    { start: TEXT.indexOf("Ember"), length: "Ember Court".length, kind: "Faction" },
];

describe("spansMatch", () => {
    it("exact needs the same start and length", () => {
        expect(spansMatch(TEXT, at(TEXT, "rellan", "Character"), GOLD[0]!, "exact")).toBe(true);
        expect(spansMatch(TEXT, at(TEXT, "the Ember Court", "Faction"), GOLD[2]!, "exact")).toBe(false);
    });

    it("overlap allows one extra or missing word at each end", () => {
        expect(spansMatch(TEXT, at(TEXT, "the Ember Court", "Faction"), GOLD[2]!, "overlap")).toBe(true);
        expect(spansMatch(TEXT, at(TEXT, "Ember Court's", "Faction"), GOLD[2]!, "overlap")).toBe(true);
        expect(spansMatch(TEXT, at(TEXT, "Keep", "Place"), GOLD[1]!, "overlap")).toBe(true);
    });

    it("overlap rejects two extra words, or spans that do not touch", () => {
        expect(spansMatch(TEXT, at(TEXT, "and the Ember Court", "Faction"), GOLD[2]!, "overlap")).toBe(false);
        expect(spansMatch(TEXT, at(TEXT, "gates", "Place"), GOLD[1]!, "overlap")).toBe(false);
    });
});

describe("matchSpans", () => {
    it("pairs one-to-one, the more confident prediction first", () => {
        const predicted = [at(TEXT, "Greyhollow", "Place", 0.6), at(TEXT, "Greyhollow Keep", "Place", 0.8)];
        const pairs = matchSpans(TEXT, GOLD, predicted, "overlap");
        expect(pairs).toEqual([[1, 1]]);
    });
});

describe("scoreNotes", () => {
    it("scores precision, recall, F1 and kind accuracy", () => {
        const predicted = [
            at(TEXT, "rellan", "Character"),
            at(TEXT, "the Ember Court", "Place"),
            at(TEXT, "spies", "Faction", 0.95),
        ];
        const score = scoreNotes([{ text: TEXT, gold: GOLD, predicted }]);
        expect(score.exact).toMatchObject({ tp: 1, predicted: 3, gold: 3 });
        expect(score.overlap).toMatchObject({ tp: 2, predicted: 3, gold: 3 });
        expect(score.overlap.precision).toBeCloseTo(2 / 3);
        expect(score.overlap.f1).toBeCloseTo(2 / 3);
        expect(score.kindAccuracy).toBe(0.5);
    });

    it("top-3 takes each note's three most confident spans", () => {
        const predicted = [
            at(TEXT, "spies", "Faction", 0.99),
            at(TEXT, "gates", "Place", 0.98),
            at(TEXT, "rellan", "Character", 0.97),
            at(TEXT, "Greyhollow Keep", "Place", 0.5),
        ];
        expect(topSpans(predicted).map((s) => s.confidence)).toEqual([0.99, 0.98, 0.97]);
        const score = scoreNotes([{ text: TEXT, gold: GOLD, predicted }]);
        expect(score.top3).toEqual({ correct: 1, shown: 3, precision: 1 / 3 });
    });

    it("an empty gold set: nothing predicted is perfect, anything predicted is a false positive", () => {
        const text = "pizza's here, break for 20 min.";
        expect(scoreNotes([{ text, gold: [], predicted: [] }]).exact.f1).toBe(1);
        expect(scoreNotes([{ text, gold: [], predicted: [] }]).top3.precision).toBeNull();
        const wrong = scoreNotes([{ text, gold: [], predicted: [at(text, "pizza", "Item")] }]);
        expect(wrong.exact.precision).toBe(0);
        expect(wrong.top3).toEqual({ correct: 0, shown: 1, precision: 0 });
        expect(wrong.kindAccuracy).toBeNull();
    });
});

describe("the invented test set", () => {
    it("has 40 notes whose gold spans sit at their offsets", () => {
        expect(notes).toHaveLength(40);
        for (const note of notes) {
            for (const g of note.gold) {
                expect(note.text.slice(g.start, g.start + g.length)).toBe(g.text);
            }
        }
    });

    it("scores itself perfectly", () => {
        const scored = notes.map((n) => ({ text: n.text, gold: n.gold, predicted: n.gold.map((g) => ({ ...g, confidence: 1 })) }));
        const score = scoreNotes(scored);
        expect(score.exact.f1).toBe(1);
        expect(score.kindAccuracy).toBe(1);
        expect(score.top3.precision).toBe(1);
    });
});
