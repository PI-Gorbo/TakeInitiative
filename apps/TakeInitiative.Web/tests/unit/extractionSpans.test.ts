import { describe, expect, it } from "vitest";
import { MAX_SPANS_PER_NOTE, distinct, finalSpans, mask, sentences, tidy, type RawSpan } from "~/utils/extraction/spans";

const at = (text: string, piece: string, kind: RawSpan["kind"] = "Character", confidence = 0.9, from = 0): RawSpan => ({
    start: text.indexOf(piece, from),
    length: piece.length,
    kind,
    confidence,
});

describe("mask", () => {
    it("keeps the length, so offsets stay the note's own", () => {
        const text = "**Rellan** met @[Mara](entry:abc-123) at `code` https://x.io/a and [the keep](https://k)\n- item\n# Head";
        const masked = mask(text);
        expect(masked.length).toBe(text.length);
        expect(masked.indexOf("Rellan")).toBe(text.indexOf("Rellan"));
        expect(masked.indexOf("item")).toBe(text.indexOf("item"));
    });

    it("blanks mentions, link targets, code, URLs and markdown syntax", () => {
        const masked = mask("**Rellan** met @[Mara](entry:abc) at `Greyhollow` https://ember.court [the keep](https://k)");
        expect(masked).not.toContain("Mara");
        expect(masked).not.toContain("Greyhollow");
        expect(masked).not.toContain("ember");
        expect(masked).not.toContain("*");
        expect(masked).not.toContain("https");
        expect(masked).toContain("the keep");
        expect(masked).toContain("Rellan");
    });

    it("keeps offsets across astral characters (UTF-16)", () => {
        const text = "🐉 **Rellan** 🗡 @[Mara](entry:x) Keep";
        const masked = mask(text);
        expect(masked.length).toBe(text.length);
        expect(masked.indexOf("Keep")).toBe(text.indexOf("Keep"));
    });
});

describe("tidy", () => {
    it("strips a leading 'the' and a trailing 's", () => {
        const text = "the Ember Court's spies";
        const [s] = tidy(text, [at(text, "the Ember Court's", "Faction")]);
        expect(s.text).toBe("Ember Court");
        expect(s.start).toBe(4);
    });

    it("drops dice, numbers and stop words", () => {
        const text = "rolled 2d6+3 and 12 then Session";
        const spans = tidy(text, [at(text, "2d6+3"), at(text, "12"), at(text, "then"), at(text, "Session")]);
        expect(spans).toEqual([]);
    });

    it("keeps the longest of overlapping spans, then the more confident", () => {
        const text = "Greyhollow Keep stands";
        const spans = tidy(text, [at(text, "Greyhollow", "Place", 0.95), at(text, "Greyhollow Keep", "Place", 0.6)]);
        expect(spans.map((s) => s.text)).toEqual(["Greyhollow Keep"]);
        const same = tidy(text, [at(text, "Greyhollow", "Place", 0.5), at(text, "Greyhollow", "Character", 0.8)]);
        expect(same).toHaveLength(1);
        expect(same[0].kind).toBe("Character");
    });
});

describe("distinct and finalSpans", () => {
    it("keeps one span per folded text, the most confident", () => {
        const text = "Rellan and rellan and Réllan";
        const spans = distinct(tidy(text, [at(text, "Rellan", "Character", 0.6), at(text, "rellan", "Character", 0.9)]));
        expect(spans).toHaveLength(1);
        expect(spans[0].text).toBe("rellan");
    });

    it("drops spans under the threshold", () => {
        const text = "Rellan met Mara";
        const spans = finalSpans(text, [at(text, "Rellan", "Character", 0.39), at(text, "Mara", "Character", 0.4)], 0.4);
        expect(spans.map((s) => s.text)).toEqual(["Mara"]);
    });

    it("caps a note at the most confident ten, in text order", () => {
        const names = Array.from({ length: 14 }, (_, i) => `Name${String.fromCharCode(65 + i)}`);
        const text = names.join(" ");
        const raw = names.map((n, i) => at(text, n, "Character", 0.5 + i * 0.03));
        const spans = finalSpans(text, raw, 0.4);
        expect(spans).toHaveLength(MAX_SPANS_PER_NOTE);
        expect(spans[0].text).toBe("NameE");
        expect(spans.map((s) => s.start)).toEqual([...spans.map((s) => s.start)].sort((a, b) => a - b));
    });

    it("never offers a span inside an existing mention", () => {
        const text = "met @[Rellan](entry:abc) at the keep";
        const masked = mask(text);
        expect(masked.includes("Rellan")).toBe(false);
    });
});

describe("sentences", () => {
    it("splits on sentence ends and newlines, covering the text", () => {
        expect(sentences("One. Two!\nThree")).toEqual([
            [0, 4],
            [4, 9],
            [10, 15],
        ]);
        expect(sentences("")).toEqual([[0, 0]]);
    });
});
