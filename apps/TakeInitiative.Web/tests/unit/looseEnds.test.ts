import { describe, expect, it } from "vitest";
import type { LooseEnd, LooseEndCounts } from "~/utils/api/types";
import {
    dividerLooseEndsLabel,
    findSpan,
    itemsInSession,
    linkSpan,
    looseEndCountLabel,
    looseEndKey,
    looseEndsHeading,
    looseEndsHref,
    mentionCountLabel,
    sessionFromQuery,
    sessionLooseEndCount,
    withHeld,
} from "~/utils/looseEnds";

const GUNDREN = "0d7e5f5e-1c1a-4a57-9c35-1a2b3c4d5e6f";
const THARDEN = "5b0c9a3e-2f77-4e11-8d42-aa11bb22cc33";

// ── linkSpan ─────────────────────────────────────────────────────────────────

describe("linkSpan", () => {
    it("turns the span at its offset into a mention, keeping the author's words", () => {
        const text = "we met gundren on the road";
        expect(linkSpan(text, { start: 7, text: "gundren" }, GUNDREN)).toBe(
            `we met @[gundren](entry:${GUNDREN}) on the road`
        );
    });

    it("links at the start and at the end of the text", () => {
        expect(linkSpan("Gundren waved", { start: 0, text: "Gundren" }, GUNDREN)).toBe(
            `@[Gundren](entry:${GUNDREN}) waved`
        );
        expect(linkSpan("we met Gundren", { start: 7, text: "Gundren" }, GUNDREN)).toBe(
            `we met @[Gundren](entry:${GUNDREN})`
        );
    });

    it("finds a span that moved, the nearest copy to where it was", () => {
        const text = "Later that night, we met gundren. Gundren? no, gundren.";
        // It was at 7 before "Later that night, " was added.
        const at = findSpan(text, { start: 7, text: "gundren" });
        expect(at).toBe(text.indexOf("gundren"));
        expect(linkSpan(text, { start: 7, text: "gundren" }, GUNDREN)).toBe(
            `Later that night, we met @[gundren](entry:${GUNDREN}). Gundren? no, gundren.`
        );
    });

    it("gives null when the span is gone", () => {
        expect(linkSpan("we met the dwarf", { start: 7, text: "gundren" }, GUNDREN)).toBeNull();
        expect(linkSpan("anything", { start: 0, text: "" }, GUNDREN)).toBeNull();
    });

    it("never links inside an existing mention", () => {
        const text = `@[Gundren](entry:${GUNDREN}) met Tharden`;
        expect(linkSpan(text, { start: 2, text: "Gundren" }, GUNDREN)).toBeNull();
        const tharden = text.indexOf("Tharden");
        expect(linkSpan(text, { start: tharden, text: "Tharden" }, THARDEN)).toBe(
            `@[Gundren](entry:${GUNDREN}) met @[Tharden](entry:${THARDEN})`
        );
    });

    it("never links part of a longer word", () => {
        expect(linkSpan("Gundrenson arrived", { start: 0, text: "Gundren" }, GUNDREN)).toBeNull();
    });

    it("counts offsets in UTF-16 code units, as the API does", () => {
        const text = "🐉 Éowyn met Gundren";
        const start = text.indexOf("Gundren");
        expect(start).toBe(13); // 🐉 is two code units
        expect(linkSpan(text, { start, text: "Gundren" }, GUNDREN)).toBe(`🐉 Éowyn met @[Gundren](entry:${GUNDREN})`);
        expect(linkSpan(text, { start: 3, text: "Éowyn" }, THARDEN)).toBe(
            `🐉 @[Éowyn](entry:${THARDEN}) met Gundren`
        );
    });
});

// ── Labels ───────────────────────────────────────────────────────────────────

describe("labels", () => {
    it("counts loose ends and mentions in words", () => {
        expect(looseEndCountLabel(1)).toBe("1 loose end");
        expect(looseEndCountLabel(3)).toBe("3 loose ends");
        expect(mentionCountLabel(1)).toBe("1 mention");
        expect(mentionCountLabel(7)).toBe("7 mentions");
        expect(dividerLooseEndsLabel(3, 12)).toBe("3 loose ends in Session 12");
    });

    it("heads the page with the total, or with one session's", () => {
        expect(looseEndsHeading(7, null)).toBe("Loose ends (7)");
        expect(looseEndsHeading(3, 12)).toBe("Session 12 · 3 loose ends");
        expect(looseEndsHeading(1, 2)).toBe("Session 2 · 1 loose end");
    });

    it("keys a row by kind and subject, so Other and empty are two rows", () => {
        const entry = { id: "e1" } as LooseEnd["entry"];
        expect(looseEndKey({ kind: "OtherKind", entry })).not.toBe(looseEndKey({ kind: "EmptyArticle", entry }));
    });
});

// ── URL and counts ───────────────────────────────────────────────────────────

describe("the page's URL", () => {
    it("builds and reads `?session=`", () => {
        expect(looseEndsHref("c 1")).toEqual({ path: "/app/campaigns/c%201/wiki/loose-ends", query: {} });
        expect(looseEndsHref("c1", { session: 12, note: "n1" })).toEqual({
            path: "/app/campaigns/c1/wiki/loose-ends",
            query: { session: "12", note: "n1" },
        });
        expect(sessionFromQuery("12")).toBe(12);
        expect(sessionFromQuery(["3"])).toBe(3);
        for (const bad of [undefined, "", "0", "-1", "1.5", "twelve"]) expect(sessionFromQuery(bad)).toBeNull();
    });

    it("narrows the items to a session by number", () => {
        const items = [
            { kind: "UnlinkedNote", sessionNumber: 12 },
            { kind: "OtherKind", sessionNumber: null },
            { kind: "UntaggedImageNote", sessionNumber: 3 },
        ] as LooseEnd[];
        expect(itemsInSession(items, null)).toHaveLength(3);
        expect(itemsInSession(items, 12).map((i) => i.kind)).toEqual(["UnlinkedNote"]);
    });

    it("reads a session's count whatever the GUID's case", () => {
        const counts: LooseEndCounts = { total: 4, bySession: { "ABC-1": 3 } };
        expect(sessionLooseEndCount(counts, "ABC-1")).toBe(3);
        expect(sessionLooseEndCount(counts, "abc-1")).toBe(3);
        expect(sessionLooseEndCount(counts, "other")).toBe(0);
        expect(sessionLooseEndCount(undefined, "abc-1")).toBe(0);
    });
});

// ── Held rows ────────────────────────────────────────────────────────────────

describe("withHeld", () => {
    const row = (id: string) => ({ kind: "UnlinkedNote", note: { id }, suggestions: [] }) as unknown as LooseEnd;

    it("puts a resolved row back where it was until it has shown its ✓", () => {
        const [a, b, c] = [row("a"), row("b"), row("c")];
        expect(withHeld([a, c], [{ item: b, index: 1 }]).map(looseEndKey)).toEqual(
            [a, b, c].map(looseEndKey)
        );
        expect(withHeld([], [{ item: b, index: 4 }]).map(looseEndKey)).toEqual([looseEndKey(b)]);
    });

    it("does not repeat a held row the list still has", () => {
        const [a, b] = [row("a"), row("b")];
        expect(withHeld([a, b], [{ item: b, index: 1 }])).toHaveLength(2);
    });
});
