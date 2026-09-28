import { describe, expect, it } from "vitest";
import type {
    EntrySummary,
    LinkSuggestion,
    SuggestionMatch,
} from "~/utils/api/types";
import type { ModelSpan } from "~/utils/extraction/spans";
import { looseEndRowLabel, suggestionCountLabel } from "~/utils/looseEnds";
import {
    acceptBody,
    batches,
    createDefaults,
    dismissKey,
    DISMISSED_KEY,
    isLinkableSpan,
    kindWithArticle,
    lookingAtLabel,
    mergeSuggestions,
    readDismissed,
    toModelSuggestions,
    inlineChipAriaLabel,
    inlineSuggestions,
    INLINE_QUEUE_MAX,
    mentionsInNotesLabel,
    revertQuestion,
    suggestedByLabel,
    withQueued,
    withDismissed,
    writeDismissed,
    type ModelSuggestion,
} from "~/utils/suggestions";

const entry = (id: string, name: string): EntrySummary => ({
    id,
    name,
    kind: "Character",
    aliases: [],
    visibility: "Everyone",
    editAccess: "Anyone",
    creatorMemberId: "m",
    createdAt: "",
    updatedAt: "",
    mergedFromIds: [],
});
const match = (id: string, name: string): SuggestionMatch => ({
    entry: entry(id, name),
    similarity: 0.9,
});
const span = (
    text: string,
    start: number,
    confidence = 0.8,
    kind: ModelSpan["kind"] = "Place"
): ModelSpan => ({
    text,
    start,
    length: text.length,
    kind,
    confidence,
});
const model = (
    s: ModelSpan,
    m: SuggestionMatch | null,
    noteId = "n1"
): ModelSuggestion => ({ ...s, noteId, match: m });
const link = (
    text: string,
    start: number,
    e: EntrySummary
): LinkSuggestion => ({
    text,
    start,
    length: text.length,
    entry: e,
    similarity: 0.9,
});

describe("isLinkableSpan", () => {
    it("takes plain words and refuses what the one-span rule refuses", () => {
        expect(isLinkableSpan("Greyhollow Keep")).toBe(true);
        expect(isLinkableSpan("")).toBe(false);
        expect(isLinkableSpan(" Rellan")).toBe(false);
        expect(isLinkableSpan("a[b]")).toBe(false);
        expect(isLinkableSpan("x`y")).toBe(false);
        expect(isLinkableSpan("two\nlines")).toBe(false);
        expect(isLinkableSpan("x".repeat(81))).toBe(false);
    });
});

describe("batches", () => {
    it("splits into requests of at most 50", () => {
        const spans = Array.from({ length: 120 }, (_, i) => i);
        expect(batches(spans).map((b) => b.length)).toEqual([50, 50, 20]);
        expect(batches([])).toEqual([]);
    });
});

describe("toModelSuggestions", () => {
    it("pairs each span with its match, in order", () => {
        const out = toModelSuggestions(
            "n1",
            [span("Rellan", 4), span("Keep", 20)],
            [match("e1", "Rellan Ashvale"), null]
        );
        expect(
            out.map((s) => [s.text, s.match?.entry.name ?? null, s.noteId])
        ).toEqual([
            ["Rellan", "Rellan Ashvale", "n1"],
            ["Keep", null, "n1"],
        ]);
    });
});

describe("mergeSuggestions", () => {
    const rellan = entry("e1", "Rellan Ashvale");

    it("adds ✨ to 19's chip for the same entry instead of a second chip", () => {
        const result = mergeSuggestions(
            [link("Rellan", 4, rellan)],
            [
                model(
                    span("rellan", 30, 0.9, "Character"),
                    match("e1", "Rellan Ashvale")
                ),
            ]
        );
        expect(result.links).toHaveLength(1);
        expect(result.links[0]!.model?.text).toBe("rellan");
        expect(result.model).toEqual([]);
        expect(result.count).toBe(1);
    });

    it("drops a create over a span 19 already links", () => {
        const result = mergeSuggestions(
            [link("Rellan", 4, rellan)],
            [model(span("Rellan", 4), null)]
        );
        expect(result.model).toEqual([]);
        expect(result.count).toBe(0);
    });

    it("keeps one chip per entry", () => {
        const result = mergeSuggestions(
            [],
            [
                model(span("Rellan", 0, 0.6), match("e1", "Rellan Ashvale")),
                model(span("Ashvale", 20, 0.9), match("e1", "Rellan Ashvale")),
            ]
        );
        expect(result.model.map((s) => s.text)).toEqual(["Ashvale"]);
    });

    it("shows at most three, matches first, then by confidence", () => {
        const result = mergeSuggestions(
            [],
            [
                model(span("A", 0, 0.99), null),
                model(span("B", 10, 0.5), match("e2", "Bee")),
                model(span("C", 20, 0.7), null),
                model(span("D", 30, 0.95), null),
                model(span("E", 40, 0.6), match("e3", "Eee")),
            ]
        );
        expect(result.model.map((s) => s.text)).toEqual(["E", "B", "A"]);
        expect(result.count).toBe(3);
    });

    it("leaves dismissed ones out", () => {
        const s = model(span("Greyhollow Keep", 0), null);
        const dismissed = new Set([dismissKey("n1", "greyhollow keep", "v1")]);
        const result = mergeSuggestions([], [s], (x) =>
            dismissed.has(dismissKey(x.noteId, x.text, "v1"))
        );
        expect(result.model).toEqual([]);
    });
});

describe("labels", () => {
    it("counts suggestions on the row label, and none leaves it plain", () => {
        expect(suggestionCountLabel(1)).toBe("1 suggestion");
        expect(looseEndRowLabel("UnlinkedNote", 2)).toBe(
            "Unlinked note · 2 suggestions"
        );
        expect(looseEndRowLabel("UnlinkedNote")).toBe("Unlinked note");
        expect(looseEndRowLabel("UntaggedImageNote", 0)).toBe("Untagged image");
    });

    it("says a or an", () => {
        expect(kindWithArticle("Place")).toBe("a Place");
        expect(kindWithArticle("Item")).toBe("an Item");
        expect(kindWithArticle("Event")).toBe("an Event");
    });

    it("says how many notes it is looking at", () => {
        expect(lookingAtLabel(1)).toBe("Looking at 1 note…");
        expect(lookingAtLabel(12)).toBe("Looking at 12 notes…");
    });
});

describe("dismissing", () => {
    it("keys by note, folded span and model version", () => {
        expect(dismissKey("ABC", "Élan Keep", "v1")).toBe("abc|elan keep|v1");
        expect(dismissKey("abc", "élan keep", "v1")).toBe(
            dismissKey("ABC", "Elan Keep", "v1")
        );
        expect(dismissKey("abc", "x", "v1")).not.toBe(
            dismissKey("abc", "x", "v2")
        );
    });

    it("moves a repeat to the end and drops the oldest past the cap", () => {
        expect(withDismissed(["a", "b"], "a")).toEqual(["b", "a"]);
        expect(withDismissed(["a", "b", "c"], "d", 3)).toEqual(["b", "c", "d"]);
    });

    it("reads and writes the list, and survives bad or blocked storage", () => {
        const store = new Map<string, string>();
        const storage = {
            getItem: (k: string) => store.get(k) ?? null,
            setItem: (k: string, v: string) => void store.set(k, v),
            removeItem: (k: string) => void store.delete(k),
        };
        writeDismissed(storage, ["a", "b"]);
        expect(readDismissed(storage)).toEqual(["a", "b"]);
        store.set(DISMISSED_KEY, "not json");
        expect(readDismissed(storage)).toEqual([]);
        const blocked = {
            getItem: () => {
                throw new Error("blocked");
            },
            setItem: () => {
                throw new Error("blocked");
            },
            removeItem: () => {},
        };
        expect(readDismissed(blocked)).toEqual([]);
        expect(() => writeDismissed(blocked, ["a"])).not.toThrow();
    });
});

describe("createDefaults", () => {
    it("takes the span as the name, the model's kind and the note's visibility", () => {
        expect(
            createDefaults(
                { text: "Greyhollow Keep", kind: "Place" },
                { visibility: "Everyone", isHidden: false }
            )
        ).toEqual({
            name: "Greyhollow Keep",
            kind: "Place",
            visibility: "Everyone",
        });
        expect(
            createDefaults(
                { text: "X", kind: "Faction" },
                { visibility: "Me", isHidden: false }
            ).visibility
        ).toBe("Me");
    });

    it("gives DM for a hidden Everyone note, as the API does", () => {
        expect(
            createDefaults(
                { text: "X", kind: "Item" },
                { visibility: "Everyone", isHidden: true }
            ).visibility
        ).toBe("DM");
    });
});

describe("acceptBody", () => {
    const m = { name: "gliner_small-v2.5", version: "v@sha+onnx-int8" };

    it("links exactly the span, with the model at its place", () => {
        const note = { text: "met rellan at the gates", isRecap: false };
        const body = acceptBody(
            note,
            { start: 4, text: "rellan", confidence: 0.87 },
            "e1",
            m
        );
        expect(body).toEqual({
            text: "met @[rellan](entry:e1) at the gates",
            isRecap: false,
            suggestion: {
                model: m.name,
                version: m.version,
                confidence: 0.87,
                start: 4,
                length: 6,
                entryId: "e1",
            },
        });
    });

    it("finds a span that moved and clamps the confidence", () => {
        const note = { text: "we met rellan", isRecap: true };
        const body = acceptBody(
            note,
            { start: 4, text: "rellan", confidence: 1.3 },
            "e1",
            m
        );
        expect(body?.suggestion.start).toBe(7);
        expect(body?.suggestion.confidence).toBe(1);
        expect(body?.isRecap).toBe(true);
    });

    it("creates the entry in the same write, keeping the author's words in the text", () => {
        const note = { text: "at Greyhollow Keep", isRecap: false };
        const body = acceptBody(
            note,
            { start: 3, text: "Greyhollow Keep", confidence: 0.7 },
            "new-id",
            m,
            { name: " The Keep ", kind: "Place" }
        );
        expect(body?.text).toBe("at @[Greyhollow Keep](entry:new-id)");
        expect(body?.newEntries).toEqual([
            { id: "new-id", name: "The Keep", kind: "Place" },
        ]);
        expect(body?.suggestion.entryId).toBe("new-id");
    });

    it("is null when the span is gone or already a mention", () => {
        expect(
            acceptBody(
                { text: "nothing here", isRecap: false },
                { start: 0, text: "rellan", confidence: 0.5 },
                "e1",
                m
            )
        ).toBeNull();
        expect(
            acceptBody(
                { text: "@[rellan](entry:e1)", isRecap: false },
                { start: 2, text: "rellan", confidence: 0.5 },
                "e1",
                m
            )
        ).toBeNull();
    });
});

describe("inline suggestions (23e)", () => {
    const rellanId = "11111111-1111-1111-1111-111111111111";
    const rellan = match(rellanId, "Rellan Ashvale");

    it("skips a match for an entry the note already mentions", () => {
        const text = `@[Rellan](entry:${rellanId.toUpperCase()}) met rellan at Greyhollow Keep`;
        const out = inlineSuggestions(text, [
            model(span("rellan", 35), rellan),
            model(span("Greyhollow Keep", 45), null),
        ]);
        expect(out.map((s) => s.text)).toEqual(["Greyhollow Keep"]);
    });

    it("drops a span that is no longer in the note's prose", () => {
        // Read before the author linked it: the span is now inside a mention.
        const text = `met @[Greyhollow Keep](entry:${rellanId})`;
        expect(
            inlineSuggestions(text, [model(span("Greyhollow Keep", 4), null)])
        ).toEqual([]);
    });

    it("keeps 23d's cap, order and dismissals", () => {
        const text = "Ash, Bree, Cole and Dunmore met rellan";
        const out = inlineSuggestions(
            text,
            [
                model(span("Ash", 0, 0.99), null),
                model(span("Bree", 5, 0.7), null),
                model(span("Cole", 11, 0.6), null),
                model(span("Dunmore", 20, 0.95), null),
                model(span("rellan", 32, 0.5), rellan),
            ],
            (s) => s.text === "Ash"
        );
        expect(out.map((s) => s.text)).toEqual(["rellan", "Dunmore", "Bree"]);
    });

    it("queues the newest request last, once, capped", () => {
        expect(withQueued(["a", "b"], "a")).toEqual(["b", "a"]);
        const full = Array.from({ length: INLINE_QUEUE_MAX }, (_, i) => `n${i}`);
        const out = withQueued(full, "new");
        expect(out).toHaveLength(INLINE_QUEUE_MAX);
        expect(out[0]).toBe("n1");
        expect(out.at(-1)).toBe("new");
    });

    it("labels the chip, the history line and the Me page", () => {
        expect(inlineChipAriaLabel(1)).toMatch(/^1 suggestion from/);
        expect(inlineChipAriaLabel(3)).toMatch(/^3 suggestions from/);
        expect(suggestedByLabel({ name: "gliner_small-v2.5", confidence: 0.871 })).toBe(
            "✨ suggested by gliner_small-v2.5 (0.87)"
        );
        expect(suggestedByLabel({ name: "m", confidence: 1.4 })).toBe("✨ suggested by m (1.00)");
        expect(mentionsInNotesLabel(14, 9)).toBe("14 mentions in 9 notes");
        expect(mentionsInNotesLabel(1, 1)).toBe("1 mention in 1 note");
        expect(revertQuestion(14)).toBe(
            "Unlink the 14 mentions this model suggested in your notes? Mentions you typed yourself stay."
        );
        expect(revertQuestion(1)).toMatch(/^Unlink the mention this model/);
    });
});
