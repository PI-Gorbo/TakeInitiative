import { describe, expect, it } from "vitest";
import type { ArticleBlock, EntryList, EntrySummary } from "~/utils/api/types";
import {
    articleRevealCheck,
    articleSaveBody,
    articleUnchanged,
    blockKind,
    canChangeBlockVisibility,
    editedBlocks,
    moveBlock,
    newBlock,
    quotesNote,
    relinkArticleNewEntry,
    removeBlock,
    secretAudience,
    secretLabel,
    setBlockVisibility,
    toEditorBlocks,
    widenToMentions,
    wrapSecret,
    type EditorBlock,
} from "~/utils/article";
import { entryDirectory } from "~/utils/entries";

const GUNDREN = "11111111-1111-4111-8111-111111111111";
const KLARG = "22222222-2222-4222-8222-222222222222";
const NEW = "33333333-3333-4333-8333-333333333333";

const me = { memberId: "me", isDm: false };
const dm = { memberId: "dm", isDm: true };

let n = 0;
const key = () => `k${++n}`;

const stored = (extra: Partial<ArticleBlock> & { id: string; text: string }): ArticleBlock => ({
    visibility: "Everyone",
    ownerMemberId: "me",
    quote: null,
    ...extra,
});

const quote = {
    noteId: "note-1",
    sessionId: "s1",
    sessionNumber: 12,
    authorMemberId: "sam",
    promotedByMemberId: "me",
    promotedAt: "2026-09-20T19:00:00Z",
};

const block = (text: string, extra: Partial<EditorBlock> = {}): EditorBlock => ({
    key: key(),
    id: "b1",
    visibility: "Everyone",
    ownerMemberId: "me",
    quote: null,
    mention: { text, links: {}, newEntries: [] },
    ...extra,
});

const summary = (
    id: string,
    name: string,
    visibility: EntrySummary["visibility"],
    creator = "other"
): EntrySummary => ({
    id,
    name,
    kind: "Character",
    aliases: [],
    visibility,
    editAccess: "Anyone",
    creatorMemberId: creator,
    createdAt: "2026-09-01T00:00:00Z",
    updatedAt: "2026-09-01T00:00:00Z",
});
const directoryOf = (...entries: EntrySummary[]) =>
    entryDirectory({
        entries: entries.map((entry) => ({
            entry,
            mentionCount: 0,
            lastMentionedAt: null,
        })),
    } as EntryList);

describe("blocks", () => {
    it("tells ordinary text, secret blocks and quotes apart", () => {
        expect(blockKind({ visibility: "Everyone" })).toBe("text");
        expect(blockKind({ visibility: "DM" })).toBe("secret");
        expect(blockKind({ visibility: "Me", quote: null })).toBe("secret");
        expect(blockKind({ visibility: "DM", quote })).toBe("quote");
    });

    it("labels secret blocks relative to their owner", () => {
        expect(secretLabel("Everyone")).toBeNull();
        expect(secretLabel("DM")).toBe("🔒 DM");
        expect(secretLabel("Me")).toBe("🔒 Me");
        expect(secretAudience("DM", "Sam", false)).toBe("Visible to the DMs and Sam");
        expect(secretAudience("DM", "Sam", true)).toBe("Visible to the DMs and you");
        expect(secretAudience("Me", "Sam", true)).toBe("Visible only to you");
    });

    it("lets only the owner and the DMs change a block's visibility", () => {
        expect(canChangeBlockVisibility({ ownerMemberId: "me" }, me)).toBe(true);
        expect(canChangeBlockVisibility({ ownerMemberId: "sam" }, me)).toBe(false);
        expect(canChangeBlockVisibility({ ownerMemberId: "sam" }, dm)).toBe(true);
        const blocks = [block("x", { ownerMemberId: "sam" })];
        expect(setBlockVisibility(blocks, 0, "DM", me)[0]!.visibility).toBe("Everyone");
        expect(setBlockVisibility(blocks, 0, "DM", dm)[0]!.visibility).toBe("DM");
    });

    it("reads the stored text back as @[Name] plus links", () => {
        const [b] = toEditorBlocks(
            [
                stored({
                    id: "b1",
                    text: `Met @[Gundren](entry:${GUNDREN}).`,
                    quote,
                }),
            ],
            key
        );
        expect(b!.id).toBe("b1");
        expect(b!.quote).toEqual(quote);
        expect(b!.mention.text).toBe("Met @[Gundren].");
        expect(b!.mention.links).toEqual({ Gundren: GUNDREN });
    });

    it("adds, moves and removes blocks", () => {
        const a = block("a");
        const b = block("b");
        const added = newBlock("secret", me, key);
        expect(added).toMatchObject({
            id: null,
            visibility: "DM",
            ownerMemberId: "me",
            quote: null,
        });
        expect(newBlock("text", me, key).visibility).toBe("Everyone");
        expect(moveBlock([a, b], 0, 1).map((x) => x.mention.text)).toEqual(["b", "a"]);
        expect(moveBlock([a, b], 0, -1).map((x) => x.mention.text)).toEqual(["a", "b"]);
        expect(removeBlock([a, b], 0).map((x) => x.mention.text)).toEqual(["b"]);
    });
});

describe("wrapSecret (🔒)", () => {
    it("splits a block around a selection, and the middle becomes the viewer's secret", () => {
        const text = "Dwarf prospector. Captured by Klarg. Brother of Tharden.";
        const start = text.indexOf("Captured");
        const end = text.indexOf(" Brother");
        const result = wrapSecret([block(text, { ownerMemberId: "sam" })], 0, { start, end }, me, key)!;
        expect(result.focusIndex).toBe(1);
        expect(result.blocks.map((b) => [b.id, b.visibility, b.ownerMemberId, b.mention.text])).toEqual([
            ["b1", "Everyone", "sam", "Dwarf prospector."],
            [null, "DM", "me", "Captured by Klarg."],
            [null, "Everyone", "me", "Brother of Tharden."],
        ]);
    });

    it("keeps the id on the part after a selection at the start", () => {
        const result = wrapSecret([block("Secret. Public.")], 0, { start: 0, end: 7 }, me, key)!;
        expect(result.focusIndex).toBe(0);
        expect(result.blocks.map((b) => [b.id, b.visibility, b.mention.text])).toEqual([
            [null, "DM", "Secret."],
            ["b1", "Everyone", "Public."],
        ]);
    });

    it("never cuts a mention in two, and each part keeps only its links", () => {
        const text = "Met @[Gundren] and @[Klarg] today";
        const b = block(text, {
            mention: {
                text,
                links: { Gundren: GUNDREN, Klarg: KLARG },
                newEntries: [],
            },
        });
        // From inside "Gundren" to inside "Klarg".
        const result = wrapSecret([b], 0, { start: text.indexOf("undren"), end: text.indexOf("arg") }, me, key)!;
        expect(result.blocks.map((x) => x.mention.text)).toEqual(["Met", "@[Gundren] and @[Klarg]", "today"]);
        expect(result.blocks[1]!.mention.links).toEqual({
            Gundren: GUNDREN,
            Klarg: KLARG,
        });
        expect(result.blocks[0]!.mention.links).toEqual({});
    });

    it("carries a pending new entry into the part that mentions it", () => {
        const text = "Old. @[Sildar] new.";
        const b = block(text, {
            mention: {
                text,
                links: { Sildar: NEW },
                newEntries: [{ id: NEW, name: "Sildar", kind: "Character" }],
            },
        });
        const result = wrapSecret([b], 0, { start: 5, end: text.length }, me, key)!;
        expect(result.blocks[0]!.mention.newEntries).toEqual([]);
        expect(result.blocks[1]!.mention.newEntries).toHaveLength(1);
    });

    it("makes the whole block secret without a selection, in place for its owner", () => {
        const result = wrapSecret([block("All of it")], 0, null, me, key, "Me")!;
        expect(result.blocks.map((b) => [b.id, b.visibility, b.ownerMemberId])).toEqual([["b1", "Me", "me"]]);
    });

    it("makes someone else's whole block a new secret block the viewer owns", () => {
        const result = wrapSecret([block("All of it", { ownerMemberId: "sam" })], 0, { start: 3, end: 3 }, me, key)!;
        expect(result.blocks.map((b) => [b.id, b.visibility, b.ownerMemberId])).toEqual([[null, "DM", "me"]]);
        // A DM keeps the block and its owner.
        const asDm = wrapSecret([block("All of it", { ownerMemberId: "sam" })], 0, null, dm, key)!;
        expect(asDm.blocks.map((b) => [b.id, b.visibility, b.ownerMemberId])).toEqual([["b1", "DM", "sam"]]);
    });

    it("treats selecting the whole text as the whole block", () => {
        const result = wrapSecret([block("  All  ")], 0, { start: 0, end: 7 }, me, key)!;
        expect(result.blocks).toHaveLength(1);
        expect(result.blocks[0]!.id).toBe("b1");
    });

    it("gives every resulting block a new key and leaves its neighbours alone", () => {
        const before = block("before", { id: "b0" });
        const target = block("x y z");
        const result = wrapSecret([before, target], 1, { start: 2, end: 3 }, me, key)!;
        expect(result.blocks[0]).toBe(before);
        expect(result.focusIndex).toBe(2);
        expect(new Set(result.blocks.map((b) => b.key)).size).toBe(4);
        expect(result.blocks.slice(1).every((b) => b.key !== target.key)).toBe(true);
    });

    it("does not split quotes or secret blocks", () => {
        expect(wrapSecret([block("q", { quote })], 0, null, me, key)).toBeNull();
        expect(wrapSecret([block("s", { visibility: "DM" })], 0, { start: 0, end: 1 }, me, key)).toBeNull();
    });

    it("widens a range to whole mentions", () => {
        expect(widenToMentions("a @[Bob] b", { start: 4, end: 6 })).toEqual({
            start: 2,
            end: 8,
        });
        expect(widenToMentions("a @[Bob] b", { start: 0, end: 1 })).toEqual({
            start: 0,
            end: 1,
        });
    });
});

describe("articleSaveBody", () => {
    it("sends every block in order, ids for existing ones, stored text", () => {
        const body = articleSaveBody("etag-1", [
            block("Met @[Gundren]", {
                mention: {
                    text: "Met @[Gundren]",
                    links: { Gundren: GUNDREN },
                    newEntries: [],
                },
            }),
            block("secret", { id: null, visibility: "DM" }),
            block("quoted", { id: "q1", quote }),
        ]);
        expect(body).toEqual({
            etag: "etag-1",
            blocks: [
                {
                    id: "b1",
                    text: `Met @[Gundren](entry:${GUNDREN})`,
                    visibility: "Everyone",
                },
                { text: "secret", visibility: "DM" },
                { id: "q1", text: "quoted", visibility: "Everyone" },
            ],
            newEntries: [],
        });
    });

    it("joins adjacent ordinary blocks, keeping the first id in the run", () => {
        const body = articleSaveBody("e", [
            block("one", { id: null }),
            block("two", { id: "b2" }),
            block("three", { id: "b3" }),
            block("secret", { id: "s", visibility: "DM" }),
            block("four", { id: "b4" }),
        ]);
        expect(body.blocks).toEqual([
            { id: "b2", text: "one\n\ntwo\n\nthree", visibility: "Everyone" },
            { id: "s", text: "secret", visibility: "DM" },
            { id: "b4", text: "four", visibility: "Everyone" },
        ]);
    });

    it("drops blank blocks, so the text on either side joins", () => {
        const body = articleSaveBody("e", [
            block("a"),
            block("  ", { id: "s", visibility: "DM" }),
            block("b", { id: "b2" }),
        ]);
        expect(body.blocks).toEqual([{ id: "b1", text: "a\n\nb", visibility: "Everyone" }]);
    });

    it("never joins quotes or secret blocks", () => {
        const body = articleSaveBody("e", [
            block("q1", { id: "q1", quote }),
            block("q2", { id: "q2", quote }),
            block("s1", { id: "s1", visibility: "DM" }),
            block("s2", { id: "s2", visibility: "DM" }),
        ]);
        expect(body.blocks.map((b) => b.id)).toEqual(["q1", "q2", "s1", "s2"]);
    });

    it("merges the new entries each block still mentions", () => {
        const entry = { id: NEW, name: "Sildar", kind: "Character" as const };
        const body = articleSaveBody("e", [
            block("@[Sildar]", {
                mention: {
                    text: "@[Sildar]",
                    links: { Sildar: NEW },
                    newEntries: [entry],
                },
            }),
            block("again @[Sildar]", {
                id: "s",
                visibility: "DM",
                mention: {
                    text: "again @[Sildar]",
                    links: { Sildar: NEW },
                    newEntries: [entry],
                },
            }),
            block("gone", {
                id: "x",
                mention: { text: "gone", links: {}, newEntries: [entry] },
            }),
        ]);
        expect(body.newEntries).toEqual([entry]);
    });

    it("knows when nothing changed", () => {
        const original = [stored({ id: "b1", text: "a" }), stored({ id: "s", text: "b", visibility: "DM" })];
        const blocks = toEditorBlocks(original, key);
        expect(articleUnchanged(original, articleSaveBody("e", blocks))).toBe(true);
        blocks[1]!.visibility = "Me";
        expect(articleUnchanged(original, articleSaveBody("e", blocks))).toBe(false);
    });
});

describe("editedBlocks (the conflict dialog)", () => {
    it("lists new and changed blocks with their stored text", () => {
        const original = [
            stored({ id: "b1", text: "same" }),
            stored({ id: "b2", text: `was @[Gundren](entry:${GUNDREN})` }),
            stored({ id: "b3", text: "moved", visibility: "DM" }),
        ];
        const blocks = toEditorBlocks(original, key);
        blocks[1]!.mention.text = "now @[Gundren]";
        blocks[2]!.visibility = "Me";
        blocks.push(newBlock("text", me, key));
        blocks[3]!.mention.text = "brand new";
        const edited = editedBlocks(original, blocks);
        expect(edited.map((e) => [e.display, e.text, e.visibility])).toEqual([
            ["now @[Gundren]", `now @[Gundren](entry:${GUNDREN})`, "Everyone"],
            ["moved", "moved", "Me"],
            ["brand new", "brand new", "Everyone"],
        ]);
    });
});

describe("relinkArticleNewEntry", () => {
    it("relinks the duplicate in every block and drops the new entry", () => {
        const entry = { id: NEW, name: "Gundren", kind: "Character" as const };
        const blocks = [
            block("@[Gundren]", {
                mention: {
                    text: "@[Gundren]",
                    links: { Gundren: NEW },
                    newEntries: [entry],
                },
            }),
            block("x @[Gundren]", {
                mention: {
                    text: "x @[Gundren]",
                    links: { Gundren: NEW },
                    newEntries: [entry],
                },
            }),
        ];
        const out = relinkArticleNewEntry(blocks, NEW, GUNDREN);
        expect(out.map((b) => b.mention.links.Gundren)).toEqual([GUNDREN, GUNDREN]);
        expect(out.every((b) => b.mention.newEntries.length === 0)).toBe(true);
    });
});

describe("articleRevealCheck", () => {
    const secretEntry = summary(KLARG, "Klarg", "DM");
    const directory = directoryOf(secretEntry, summary(GUNDREN, "Gundren", "Everyone"));
    const text = `@[Klarg](entry:${KLARG})`;

    it("warns when an ordinary block of an Everyone entry mentions a DM entry", () => {
        const result = articleRevealCheck("Everyone", [{ text, visibility: "Everyone" }], directory, me);
        expect(result.audience).toBe("Everyone");
        expect(result.items.map((i) => i.entry.name)).toEqual(["Klarg"]);
    });

    it("does not warn inside a 🔒 DM block, or in a DM entry's article", () => {
        expect(articleRevealCheck("Everyone", [{ text, visibility: "DM" }], directory, me).items).toEqual([]);
        expect(articleRevealCheck("DM", [{ text, visibility: "Everyone" }], directory, me).items).toEqual([]);
    });

    it("lists each entry once and names the widest audience", () => {
        const result = articleRevealCheck(
            "Everyone",
            [
                { text, visibility: "Everyone" },
                { text, visibility: "Everyone" },
            ],
            directory,
            dm
        );
        expect(result.items).toHaveLength(1);
        expect(result.items[0]!.canReveal).toBe(true);
    });
});

describe("quotesNote", () => {
    it("finds an earlier quote of the same note", () => {
        const blocks = [stored({ id: "q", text: "x", quote })];
        expect(quotesNote(blocks, "NOTE-1")).toBe(true);
        expect(quotesNote(blocks, "note-2")).toBe(false);
        expect(quotesNote(undefined, "note-1")).toBe(false);
    });
});
