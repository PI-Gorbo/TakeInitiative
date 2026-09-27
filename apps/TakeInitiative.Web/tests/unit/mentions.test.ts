import { describe, expect, it } from "vitest";
import type { EntryListItem, EntrySummary } from "~/utils/api/types";
import { entryDirectory } from "~/utils/entries";
import { noteMentions } from "~/utils/markdown";
import {
    aboutPrefill,
    activeMention,
    createEntry,
    displayTokens,
    escapeMentionText,
    fromStoredText,
    insertMentionTrigger,
    linkedMentions,
    matchEntries,
    mentionBody,
    mentionSuggestions,
    newEntryErrorFrom,
    orphanedLinkIds,
    parseDraft,
    pendingEntrySummary,
    pickEntry,
    pruneMentionText,
    relinkNewEntry,
    revealCheck,
    revealMessage,
    serializeDraft,
    toStoredText,
    unescapeMentionText,
    type ActiveMention,
    type MentionText,
} from "~/utils/mentions";

const GUNDREN = "0b7c5e1a-8f3d-4c2b-9a61-2d4e8f00a001";
const PHANDALIN = "0b7c5e1a-8f3d-4c2b-9a61-2d4e8f00a002";
const GLASSTAFF = "0b7c5e1a-8f3d-4c2b-9a61-2d4e8f00a003";
const SILDAR = "0b7c5e1a-8f3d-4c2b-9a61-2d4e8f00a004";
const NEW_ID = "0b7c5e1a-8f3d-4c2b-9a61-2d4e8f00a0ff";
const CREATOR = "member-creator";

function summary(id: string, name: string, extra: Partial<EntrySummary> = {}): EntrySummary {
    return {
        id,
        name,
        kind: "Character",
        aliases: [],
        mergedFromIds: [],
        visibility: "Everyone",
        editAccess: "Anyone",
        creatorMemberId: CREATOR,
        createdAt: "2026-09-01T18:00:00Z",
        updatedAt: "2026-09-01T18:00:00Z",
        ...extra,
    };
}
const item = (entry: EntrySummary, mentionCount = 0): EntryListItem => ({ entry, mentionCount, lastMentionedAt: null });

const directory = entryDirectory({
    entries: [
        item(summary(GUNDREN, "Gundren Rockseeker", { aliases: ["Rockseeker"] }), 7),
        item(summary(PHANDALIN, "Phandalin", { kind: "Place" }), 3),
        item(summary(GLASSTAFF, "Glasstaff", { visibility: "DM" }), 1),
        item(summary(SILDAR, "Sildar Hallwinter", { visibility: "Me" }), 0),
    ],
});

/** "We met @Gu|nd" → the text without the "|" marker, and the caret where it was. */
function withCaret(marked: string): [string, number] {
    const caret = marked.indexOf("|");
    return [marked.slice(0, caret) + marked.slice(caret + 1), caret];
}

const state = (text: string, links: Record<string, string> = {}, newEntries: MentionText["newEntries"] = []) => ({
    text,
    links,
    newEntries,
});

describe("stored form and composer form", () => {
    it("writes each linked @[text] as @[text](entry:<id>)", () => {
        expect(toStoredText("We met @[Gundren] in @[Phandalin].", { Gundren: GUNDREN, Phandalin: PHANDALIN })).toBe(
            `We met @[Gundren](entry:${GUNDREN}) in @[Phandalin](entry:${PHANDALIN}).`
        );
    });

    it("leaves unlinked, escaped and already-stored text alone", () => {
        const links = { Gundren: GUNDREN };
        expect(toStoredText("@[Nobody] and \\@[Gundren]", links)).toBe("@[Nobody] and \\@[Gundren]");
        expect(toStoredText(`@[Gundren](entry:${PHANDALIN})`, links)).toBe(`@[Gundren](entry:${PHANDALIN})`);
        expect(toStoredText("\\\\@[Gundren]", links)).toBe(`\\\\@[Gundren](entry:${GUNDREN})`);
    });

    it("reads the stored form back", () => {
        expect(fromStoredText(`Met @[the old dwarf](entry:${GUNDREN}), then @[Phandalin](entry:${PHANDALIN}).`)).toEqual({
            text: "Met @[the old dwarf], then @[Phandalin].",
            links: { "the old dwarf": GUNDREN, Phandalin: PHANDALIN },
        });
    });

    it("keeps a second entry under the same display text in the stored form", () => {
        const stored = `@[him](entry:${GUNDREN}) and @[him](entry:${SILDAR}) and @[him](entry:${GUNDREN.toUpperCase()})`;
        const read = fromStoredText(stored);
        expect(read.text).toBe(`@[him] and @[him](entry:${SILDAR}) and @[him]`);
        expect(read.links).toEqual({ him: GUNDREN });
        expect(toStoredText(read.text, read.links)).toBe(
            `@[him](entry:${GUNDREN}) and @[him](entry:${SILDAR}) and @[him](entry:${GUNDREN})`
        );
    });

    it("round-trips any stored text", () => {
        const cases = [
            "",
            "no mentions at all",
            `@[Gundren](entry:${GUNDREN})`,
            `\\@[Gundren](entry:${GUNDREN}) is escaped`,
            `a @[Salt \\& Pepper \\[x\\]](entry:${GUNDREN}) b`,
            `@[x](entry:not-a-guid) @[y](https://example.com)`,
            `@[A](entry:${GUNDREN})@[A](entry:${PHANDALIN})\n@[B](entry:${GLASSTAFF})`,
        ];
        for (const stored of cases) {
            const { text, links } = fromStoredText(stored);
            expect(toStoredText(text, links)).toBe(stored);
        }
    });

    it("escapes a name so the chip reads back the same text", () => {
        for (const name of ["Salt & Pepper", "The [Red] Wizard", "a_b*c`d\\e<f>~"]) {
            const raw = escapeMentionText(name);
            expect(unescapeMentionText(raw)).toBe(name);
            const stored = toStoredText(`@[${raw}]`, { [raw]: GUNDREN });
            expect(noteMentions(stored)).toEqual([{ entryId: GUNDREN, text: name }]);
            expect(displayTokens(`@[${raw}]`)).toHaveLength(1);
        }
    });
});

describe("mentionBody", () => {
    it("drops new entries whose mention was deleted, and keeps the rest", () => {
        const s = state("@[Glass] only", { Glass: NEW_ID, Gone: SILDAR }, [
            { id: NEW_ID, name: "Glass", kind: "Item" },
            { id: SILDAR, name: "Gone", kind: "Other" },
        ]);
        expect(mentionBody(s)).toEqual({
            text: `@[Glass](entry:${NEW_ID}) only`,
            newEntries: [{ id: NEW_ID, name: "Glass", kind: "Item" }],
        });
    });

    it("does not count a mention inside code, as the API does not", () => {
        const s = state("`@[Glass]`", { Glass: NEW_ID }, [{ id: NEW_ID, name: "Glass", kind: "Item" }]);
        expect(mentionBody(s).newEntries).toEqual([]);
    });
});

describe("activeMention", () => {
    const at = (text: string, links: Record<string, string> = {}) => activeMention(...withCaret(text), links);

    it("finds a typed query at the start, after a space or after (", () => {
        expect(at("@Gund|")).toEqual({ kind: "typed", start: 0, end: 5, query: "Gund" });
        expect(at("We met @Gundren Rock| there")).toEqual({ kind: "typed", start: 7, end: 20, query: "Gundren Rock" });
        expect(at("(@Ph|")).toMatchObject({ query: "Ph", start: 1 });
        expect(at("hi @|")).toMatchObject({ query: "" });
    });

    it("ignores emails, escapes, newlines, long runs and a leading space", () => {
        expect(at("sam@example|")).toBeNull();
        expect(at("\\@Gund|")).toBeNull();
        expect(at("@Gund\nren|")).toBeNull();
        expect(at(`@${"x".repeat(41)}|`)).toBeNull();
        expect(at(`@${"x".repeat(40)}|`)).not.toBeNull();
        expect(at("@ Gund|")).toBeNull();
    });

    it("treats an unclosed @[ as a typed query", () => {
        expect(at("@[Gund|")).toEqual({ kind: "typed", start: 0, end: 6, query: "Gund" });
    });

    it("is closed right after a mention", () => {
        expect(at("@[Gundren]|", { Gundren: GUNDREN })).toBeNull();
        expect(at("@[Gundren] and |", { Gundren: GUNDREN })).toBeNull();
    });

    it("uses the bracket text of an unlinked @[…] as the query", () => {
        expect(at("Met @[the old| dwarf] today")).toEqual({
            kind: "bracket",
            start: 4,
            end: 20,
            query: "the old dwarf",
            raw: "the old dwarf",
        });
        expect(at("@[Salt \\& Pep|per]")).toMatchObject({ kind: "bracket", query: "Salt & Pepper" });
        expect(at("@[Gund|ren]", { Gundren: GUNDREN })).toBeNull();
    });
});

describe("matchEntries", () => {
    const names = (query: string) => matchEntries(query, directory).map((m) => m.entry.name);

    it("ranks exact, prefix, word prefix, then substring", () => {
        const d = entryDirectory({
            entries: [
                item(summary("a", "Old Rock"), 0),
                item(summary("b", "Rockport"), 0),
                item(summary("c", "Rock"), 0),
                item(summary("d", "Bedrock"), 9),
            ],
        });
        expect(matchEntries("rock", d).map((m) => m.entry.name)).toEqual(["Rock", "Rockport", "Old Rock", "Bedrock"]);
    });

    it("puts the most mentioned first among equals, then A–Z", () => {
        const d = entryDirectory({
            entries: [item(summary("a", "Gb"), 1), item(summary("b", "Ga"), 1), item(summary("c", "Gc"), 5)],
        });
        expect(matchEntries("g", d).map((m) => m.entry.name)).toEqual(["Gc", "Ga", "Gb"]);
    });

    it("matches aliases and says which one", () => {
        expect(matchEntries("Rock", directory)[0]).toMatchObject({ entry: { name: "Gundren Rockseeker" }, alias: "Rockseeker" });
        expect(matchEntries("Gund", directory)[0]!.alias).toBeUndefined();
    });

    it("ignores case, accents and extra spaces", () => {
        expect(names("  gÜNDREN   rock")).toEqual(["Gundren Rockseeker"]);
    });

    it("lists the most mentioned for an empty query, at most 8", () => {
        expect(names("")).toEqual(["Gundren Rockseeker", "Phandalin", "Glasstaff", "Sildar Hallwinter"]);
        const many = entryDirectory({ entries: Array.from({ length: 12 }, (_, i) => item(summary(`${i}`, `E${i}`))) });
        expect(matchEntries("", many)).toHaveLength(8);
    });
});

describe("mentionSuggestions", () => {
    it("offers Create when no name or alias equals the query", () => {
        expect(mentionSuggestions("Glass", directory).at(-1)).toEqual({ kind: "create", name: "Glass" });
        expect(mentionSuggestions("glasstaff", directory).some((s) => s.kind === "create")).toBe(false);
        expect(mentionSuggestions("rockseeker", directory).some((s) => s.kind === "create")).toBe(false);
        expect(mentionSuggestions("", directory).some((s) => s.kind === "create")).toBe(false);
    });

    it("closes a query that ends with a space and matches nothing", () => {
        expect(mentionSuggestions("xyz ", directory)).toEqual([]);
        expect(mentionSuggestions("Gundren ", directory)[0]).toMatchObject({ entryId: GUNDREN });
    });

    it("suggests this text's new entries, marked new, and no second Create for them", () => {
        const pending = [{ id: NEW_ID, name: "Cragmaw", kind: "Place" as const }];
        const list = mentionSuggestions("crag", directory, pending);
        expect(list[0]).toEqual({ kind: "entry", entryId: NEW_ID, name: "Cragmaw", entryKind: "Place", isNew: true });
        expect(mentionSuggestions("Cragmaw", directory, pending).some((s) => s.kind === "create")).toBe(false);
    });

    it("puts preferred ids first, for re-picking after overriding the text", () => {
        const list = mentionSuggestions("the old dwarf", directory, [], [GUNDREN]);
        expect(list[0]).toMatchObject({ kind: "entry", entryId: GUNDREN });
        expect(list.at(-1)).toEqual({ kind: "create", name: "the old dwarf" });
    });

    it("does not offer Create for a name over the API's limit", () => {
        expect(mentionSuggestions("x".repeat(101), directory)).toEqual([]);
    });
});

describe("picking", () => {
    const typed = (text: string): ActiveMention => activeMention(...withCaret(text), {})!;

    it("replaces a typed query with @[Name] and a space, and links it", () => {
        const active = typed("We met @Gund|");
        const next = pickEntry(state("We met @Gund"), active, { id: GUNDREN, name: "Gundren Rockseeker" });
        expect(next.text).toBe("We met @[Gundren Rockseeker] ");
        expect(next.caret).toBe(next.text.length);
        expect(next.links).toEqual({ "Gundren Rockseeker": GUNDREN });
    });

    it("adds no space when one follows", () => {
        const text = "@Ph there";
        const next = pickEntry(state(text), activeMention(text, 3, {})!, { id: PHANDALIN, name: "Phandalin" });
        expect(next.text).toBe("@[Phandalin] there");
        expect(next.caret).toBe(12);
    });

    it("links a bracket query without changing the text (the display-text override)", () => {
        const text = "Met @[the old dwarf] today";
        const active = activeMention(text, 10, {})!;
        const next = pickEntry(state(text), active, { id: GUNDREN, name: "Gundren Rockseeker" });
        expect(next.text).toBe(text);
        expect(next.links).toEqual({ "the old dwarf": GUNDREN });
        expect(toStoredText(next.text, next.links)).toBe(`Met @[the old dwarf](entry:${GUNDREN}) today`);
    });

    it("writes the stored form when the display text already links another entry", () => {
        const s = state("@[Gundren Rockseeker] and @Gu", { "Gundren Rockseeker": SILDAR });
        const next = pickEntry(s, activeMention(s.text, s.text.length, s.links)!, { id: GUNDREN, name: "Gundren Rockseeker" });
        expect(next.text).toBe(`@[Gundren Rockseeker] and @[Gundren Rockseeker](entry:${GUNDREN}) `);
        expect(next.links).toEqual({ "Gundren Rockseeker": SILDAR });
    });

    it("creates a new entry with a client id, linked like a pick", () => {
        const next = createEntry(state("Sildar said @Glasst"), typed("Sildar said @Glasst|"), "Character", NEW_ID);
        expect(next.text).toBe("Sildar said @[Glasst] ");
        expect(next.newEntries).toEqual([{ id: NEW_ID, name: "Glasst", kind: "Character" }]);
        expect(mentionBody(next)).toEqual({
            text: `Sildar said @[Glasst](entry:${NEW_ID})`.concat(" "),
            newEntries: [{ id: NEW_ID, name: "Glasst", kind: "Character" }],
        });
    });

    it("creates from a bracket query using its plain text as the name", () => {
        const text = "@[Salt \\& Pepper]";
        const next = createEntry(state(text), activeMention(text, 5, {})!, "Item", NEW_ID);
        expect(next.text).toBe(text);
        expect(next.newEntries[0]!.name).toBe("Salt & Pepper");
        expect(noteMentions(mentionBody(next).text)).toEqual([{ entryId: NEW_ID, text: "Salt & Pepper" }]);
    });
});

describe("insertMentionTrigger (the @ button)", () => {
    it("adds a space before @ unless at a line start or after a space or (", () => {
        expect(insertMentionTrigger({ text: "We met", selectionStart: 6, selectionEnd: 6 })).toEqual({
            text: "We met @",
            selectionStart: 8,
            selectionEnd: 8,
        });
        expect(insertMentionTrigger({ text: "", selectionStart: 0, selectionEnd: 0 }).text).toBe("@");
        expect(insertMentionTrigger({ text: "a\n", selectionStart: 2, selectionEnd: 2 }).text).toBe("a\n@");
        expect(insertMentionTrigger({ text: "(", selectionStart: 1, selectionEnd: 1 }).text).toBe("(@");
    });

    it("makes a selection the query", () => {
        const next = insertMentionTrigger({ text: "met Gundren", selectionStart: 4, selectionEnd: 11 });
        expect(next).toEqual({ text: "met @Gundren", selectionStart: 12, selectionEnd: 12 });
        expect(activeMention(next.text, next.selectionStart, {})).toMatchObject({ query: "Gundren" });
    });
});

describe("links bookkeeping", () => {
    it("finds links whose text was edited away, and prunes them", () => {
        const s = state("@[the old dwarf]", { Gundren: GUNDREN }, [{ id: NEW_ID, name: "X", kind: "Other" }]);
        expect(orphanedLinkIds(s)).toEqual([GUNDREN]);
        expect(pruneMentionText(s)).toEqual({ text: "@[the old dwarf]", links: {}, newEntries: [] });
    });

    it("lists the linked entries in order, new ones marked, unknown ones left out", () => {
        const s = state("@[Ph] @[New] @[Ghost] @[Ph]", { Ph: PHANDALIN, New: NEW_ID, Ghost: "0b7c5e1a-8f3d-4c2b-9a61-2d4e8f00a0ee" }, [
            { id: NEW_ID, name: "New", kind: "Item" },
        ]);
        expect(linkedMentions(s, directory)).toEqual([
            { entryId: PHANDALIN, name: "Phandalin", entryKind: "Place", isNew: false },
            { entryId: NEW_ID, name: "New", entryKind: "Item", isNew: true },
        ]);
    });
});

describe("aboutPrefill", () => {
    const entry = { id: GUNDREN, name: "Gundren", visibility: "Everyone" as const };

    it("starts the text with @[Name] and links it", () => {
        expect(aboutPrefill({ text: "", links: {} }, entry)).toEqual({
            text: "@[Gundren] ",
            links: { Gundren: GUNDREN },
            visibility: undefined,
        });
        expect(aboutPrefill({ text: "  a draft", links: {} }, entry).text).toBe("@[Gundren] a draft");
    });

    it("does not add the mention twice", () => {
        expect(aboutPrefill({ text: "@[Gundren] more", links: { Gundren: GUNDREN } }, entry).text).toBe("@[Gundren] more");
    });

    it("matches a DM or Me entry's visibility", () => {
        expect(aboutPrefill({ text: "", links: {} }, { ...entry, visibility: "DM" }).visibility).toBe("DM");
        expect(aboutPrefill({ text: "", links: {} }, { ...entry, visibility: "Me" }).visibility).toBe("Me");
    });

    it("writes the stored form when the name already links another entry", () => {
        const next = aboutPrefill({ text: "@[Gundren]", links: { Gundren: SILDAR } }, entry);
        expect(next.text).toBe(`@[Gundren](entry:${GUNDREN}) @[Gundren]`);
        expect(next.links).toEqual({ Gundren: SILDAR });
    });
});

describe("revealCheck", () => {
    const stored = (...ids: string[]) => ids.map((id, i) => `@[e${i}](entry:${id})`).join(" ");
    const dm = { memberId: "dm", isDm: true };
    const player = { memberId: "p", isDm: false };
    const creator = { memberId: CREATOR, isDm: false };

    it("lists DM and Me entries in an Everyone note", () => {
        const items = revealCheck("Everyone", stored(GUNDREN, GLASSTAFF, SILDAR), directory, dm);
        expect(items.map((i) => i.entry.name)).toEqual(["Glasstaff", "Sildar Hallwinter"]);
    });

    it("lists Me entries in a DM note, and nothing in a Me note", () => {
        expect(revealCheck("DM", stored(GLASSTAFF, SILDAR), directory, dm).map((i) => i.entry.id)).toEqual([SILDAR]);
        expect(revealCheck("Me", stored(GLASSTAFF, SILDAR), directory, dm)).toEqual([]);
    });

    it("ignores entries not in the directory (new ones, unknown ones)", () => {
        expect(revealCheck("Everyone", stored(NEW_ID), directory, dm)).toEqual([]);
    });

    it("offers Reveal only to the creator and the DMs", () => {
        expect(revealCheck("Everyone", stored(GLASSTAFF), directory, dm)[0]!.canReveal).toBe(true);
        expect(revealCheck("Everyone", stored(GLASSTAFF), directory, creator)[0]!.canReveal).toBe(true);
        expect(revealCheck("Everyone", stored(GLASSTAFF), directory, player)[0]!.canReveal).toBe(false);
    });

    it("words the warning", () => {
        const items = revealCheck("Everyone", stored(GLASSTAFF, SILDAR), directory, dm);
        expect(revealMessage(items.slice(0, 1), "Everyone")).toBe("Glasstaff is hidden from players.");
        expect(revealMessage(items, "Everyone")).toBe("Glasstaff and Sildar Hallwinter are hidden from players.");
        expect(revealMessage(items.slice(1), "DM")).toBe("Sildar Hallwinter is hidden from the DMs.");
    });
});

describe("new entry errors", () => {
    const failure = (status: number, errors: Record<string, string[]>) => ({ response: { status, data: { errors } } });

    it("reads the three API errors", () => {
        expect(newEntryErrorFrom(failure(409, { alreadyCreatedEntryId: [NEW_ID] }))).toEqual({
            kind: "alreadyCreated",
            entryId: NEW_ID,
        });
        expect(
            newEntryErrorFrom(failure(409, { name: ["taken"], existingEntryId: [GUNDREN], newEntryId: [NEW_ID] }))
        ).toEqual({ kind: "duplicate", newEntryId: NEW_ID, existingEntryId: GUNDREN });
        expect(newEntryErrorFrom(failure(400, { newEntries: ["not mentioned"] }))).toEqual({
            kind: "unmentioned",
            message: "not mentioned",
        });
        expect(newEntryErrorFrom(failure(400, { text: ["x"] }))).toBeNull();
        expect(newEntryErrorFrom(new Error("network"))).toBeNull();
    });

    it("relinks a duplicate to the entry that has the name", () => {
        const s = state("@[Gundren] @[Ph]", { Gundren: NEW_ID, Ph: PHANDALIN }, [{ id: NEW_ID, name: "Gundren", kind: "Character" }]);
        expect(relinkNewEntry(s, NEW_ID.toUpperCase(), GUNDREN)).toEqual({
            text: s.text,
            links: { Gundren: GUNDREN, Ph: PHANDALIN },
            newEntries: [],
        });
    });
});

describe("drafts", () => {
    it("round-trips text, links and new entries, pruning unused ones", () => {
        const draft = state("@[New] @[Ph]", { New: NEW_ID, Ph: PHANDALIN, Gone: GUNDREN }, [
            { id: NEW_ID, name: "New", kind: "Item" },
        ]);
        expect(parseDraft(serializeDraft(draft))).toEqual({ ...draft, links: { New: NEW_ID, Ph: PHANDALIN } });
    });

    it("loads a 14d string draft, including one that starts with {", () => {
        expect(parseDraft("half a thought")).toEqual(state("half a thought"));
        expect(parseDraft("{not json")).toEqual(state("{not json"));
        expect(parseDraft('{"other": 1}')).toEqual(state('{"other": 1}'));
        expect(parseDraft(null)).toEqual(state(""));
    });

    it("drops malformed links and new entries", () => {
        expect(parseDraft(JSON.stringify({ text: "x", links: { a: 1, b: "id" }, newEntries: [{ id: 1 }, null] }))).toEqual(
            state("x", { b: "id" })
        );
    });
});

describe("pendingEntrySummary", () => {
    it("is the new entry with the note's visibility and its author as creator", () => {
        const now = new Date("2026-09-25T18:00:00Z");
        expect(
            pendingEntrySummary({ id: NEW_ID, name: "Glasstaff", kind: "Character" }, { creatorMemberId: "m1", visibility: "DM", now })
        ).toEqual({
            id: NEW_ID,
            name: "Glasstaff",
            kind: "Character",
            aliases: [],
            mergedFromIds: [],
            visibility: "DM",
            editAccess: "Anyone",
            creatorMemberId: "m1",
            createdAt: now.toISOString(),
            updatedAt: now.toISOString(),
        });
    });
});
