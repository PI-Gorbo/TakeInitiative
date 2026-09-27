import { describe, expect, it } from "vitest";
import type { EntryList, EntrySummary } from "~/utils/api/types";
import { entryDirectory } from "~/utils/entries";
import {
    collapseWhitespace,
    isExcerptOf,
    normalizeForMatch,
    noteAudience,
    plainView,
    promoteTargets,
    selectionToSource,
} from "~/utils/promote";

const GUNDREN = "11111111-1111-4111-8111-111111111111";
const mention = `@[Gundren Rockseeker](entry:${GUNDREN})`;

/** What the server accepts (`PostEntryQuote`): a substring once whitespace is collapsed. */
const accepted = (source: string, text: string) => collapseWhitespace(source).includes(collapseWhitespace(text));

describe("plainView", () => {
    it("drops mention and link syntax, keeping the text, with an offset map", () => {
        const source = `Met ${mention} and [the map](https://x.io) today`;
        const view = plainView(source);
        expect(view.text).toBe("Met Gundren Rockseeker and the map today");
        expect(source[view.map[view.text.indexOf("Gundren")]!]).toBe("G");
        expect(view.spans).toHaveLength(2);
    });

    it("drops list, quote and heading markers and code fences", () => {
        expect(plainView("# Title\n- one\n2. two\n> quoted").text).toBe("Title\none\ntwo\nquoted");
        expect(plainView("```js\ncode\n```").text).toBe("\ncode\n");
    });

    it("drops escapes' backslashes", () => {
        expect(plainView(String.raw`a \*b\* c`).text).toBe("a *b* c");
    });
});

describe("normalizeForMatch", () => {
    it("collapses whitespace and ignores markup characters and kind icons", () => {
        expect(normalizeForMatch("  a **b**\n\n  _c_ `d`").text).toBe("a b c d");
        expect(normalizeForMatch("met 👤Gundren and 🛡️Zhents").text).toBe("met Gundren and Zhents");
    });
});

describe("selectionToSource", () => {
    it("maps a plain selection back to the source", () => {
        const source = "We met him on the road to town.";
        expect(selectionToSource(source, "on the road")).toMatchObject({
            text: "on the road",
        });
    });

    it("widens a selection that starts or ends inside a chip to the whole mention", () => {
        const source = `We met ${mention} on the road.`;
        // The chip is drawn with its icon; the reader selected part of it.
        const result = selectionToSource(source, "Rockseeker on the")!;
        expect(result.text).toBe(`${mention} on the`);
        expect(accepted(source, result.text)).toBe(true);
        expect(selectionToSource(source, "met 👤Gundren")!.text).toBe(`met ${mention}`);
    });

    it("keeps emphasis inside the selection and matches across it", () => {
        const source = "A **bold** claim and _more_.";
        const result = selectionToSource(source, "bold claim and more")!;
        expect(result.text).toBe("bold** claim and _more");
        expect(accepted(source, result.text)).toBe(true);
    });

    it("matches across paragraphs and list items", () => {
        const source = "First line.\n\n- item one\n- item two";
        const result = selectionToSource(source, "line.\n\nitem one\nitem")!;
        expect(result.text).toBe("line.\n\n- item one\n- item");
        expect(accepted(source, result.text)).toBe(true);
    });

    it("keeps a link whole", () => {
        const source = "See [the map](https://x.io) for more";
        expect(selectionToSource(source, "the ma")!.text).toBe("[the map](https://x.io)");
    });

    it("handles escaped characters and underscores in words", () => {
        const source = String.raw`a \*literal\* snake_case word`;
        const result = selectionToSource(source, "*literal* snake_case")!;
        expect(accepted(source, result.text)).toBe(true);
        // Markup characters at the edges are optional on both sides, so the match
        // starts at the first letter.
        expect(result.text).toBe(String.raw`literal\* snake_case`);
    });

    it("gives null for text that is not in the note", () => {
        expect(selectionToSource("Hello there", "General Kenobi")).toBeNull();
        expect(selectionToSource("Hello there", "  \n ")).toBeNull();
    });

    it("maps a selection ending in an emoji", () => {
        const source = "Found a 🐉 egg";
        expect(selectionToSource(source, "a 🐉")!.text).toBe("a 🐉");
    });
});

describe("isExcerptOf", () => {
    it("is the server's substring check", () => {
        expect(isExcerptOf("a  b\nc", "b c")).toBe(true);
        expect(isExcerptOf("a b c", "c d")).toBe(false);
        expect(isExcerptOf("a b c", "  ")).toBe(false);
    });
});

const entry = (id: string, name: string, extra: Partial<EntrySummary> = {}): EntrySummary => ({
    id,
    name,
    kind: "Character",
    aliases: [],
    mergedFromIds: [],
    visibility: "Everyone",
    editAccess: "Anyone",
    creatorMemberId: "other",
    createdAt: "2026-09-01T00:00:00Z",
    updatedAt: "2026-09-01T00:00:00Z",
    ...extra,
});

describe("promoteTargets", () => {
    const directory = entryDirectory({
        entries: [
            {
                entry: entry("a", "Gundren Rockseeker", {
                    aliases: ["Rockseeker"],
                }),
                mentionCount: 3,
                lastMentionedAt: null,
            },
            {
                entry: entry("b", "Glasstaff", { editAccess: "OnlyMe" }),
                mentionCount: 5,
                lastMentionedAt: null,
            },
            {
                entry: entry("c", "Phandalin", { kind: "Place" }),
                mentionCount: 1,
                lastMentionedAt: null,
            },
        ],
    } as EntryList);
    const player = { memberId: "me", isDm: false };

    it("offers only the entries the viewer can edit, most mentioned first", () => {
        expect(promoteTargets("", directory, player).map((t) => t.kind === "entry" && t.entry.name)).toEqual([
            "Gundren Rockseeker",
            "Phandalin",
        ]);
        expect(promoteTargets("", directory, { memberId: "me", isDm: true })).toHaveLength(3);
    });

    it("matches aliases and offers Create last when nothing is called that", () => {
        const targets = promoteTargets("Rock", directory, player);
        expect(targets[0]).toMatchObject({
            kind: "entry",
            alias: "Rockseeker",
        });
        expect(targets[targets.length - 1]).toEqual({
            kind: "create",
            name: "Rock",
        });
    });

    it("never offers Create for a name a visible entry has, even one the viewer cannot edit", () => {
        expect(promoteTargets("glasstaff", directory, player)).toEqual([]);
        expect(promoteTargets("rockseeker", directory, player).some((t) => t.kind === "create")).toBe(false);
    });
});

describe("noteAudience", () => {
    it("reads a hidden Everyone note as DM", () => {
        expect(noteAudience({ visibility: "Everyone", isHidden: true })).toBe("DM");
        expect(noteAudience({ visibility: "Everyone", isHidden: false })).toBe("Everyone");
        expect(noteAudience({ visibility: "Me", isHidden: true })).toBe("Me");
    });
});
