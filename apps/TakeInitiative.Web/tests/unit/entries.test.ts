import { describe, expect, it } from "vitest";
import type { EntryList, EntryListItem, EntrySummary } from "~/utils/api/types";
import {
    ENTRY_ALIASES_MAX,
    ENTRY_KINDS,
    aboutPrefill,
    addAlias,
    aliasesLabel,
    canChangeEntryAccess,
    canEditEntry,
    editAccessLabel,
    entriesCalled,
    entryDirectory,
    existingEntryIdFrom,
    filterEntries,
    kindFromQuery,
    kindToQuery,
    mentionCountLabel,
    mentionMarkup,
    resolveEntry,
    sortEntries,
    sortFromQuery,
    sortToQuery,
} from "~/utils/entries";
import { noteMentions } from "~/utils/markdown";

const CREATOR = "member-creator";
const OTHER = "member-other";

function summary(id: string, name: string, extra: Partial<EntrySummary> = {}): EntrySummary {
    return {
        id,
        name,
        kind: "Character",
        aliases: [],
        visibility: "Everyone",
        editAccess: "Anyone",
        creatorMemberId: CREATOR,
        createdAt: "2026-09-01T18:00:00Z",
        updatedAt: "2026-09-01T18:00:00Z",
        ...extra,
    };
}
const item = (entry: EntrySummary, mentionCount = 0, lastMentionedAt: string | null = null): EntryListItem => ({
    entry,
    mentionCount,
    lastMentionedAt,
});

const gundren = item(summary("a1", "Gundren Rockseeker", { aliases: ["Rockseeker"] }), 7, "2026-09-10T19:00:00Z");
const phandalin = item(summary("b2", "Phandalin", { kind: "Place" }), 7, "2026-09-12T19:00:00Z");
const klarg = item(summary("c3", "klarg", { kind: "Character" }), 2, "2026-09-20T19:00:00Z");
const tharden = item(summary("d4", "Tharden"), 0, null);
const list: EntryList = { entries: [gundren, phandalin, klarg, tharden] };

describe("the entry directory", () => {
    it("resolves ids ignoring case, and misses ids the viewer cannot see", () => {
        const directory = entryDirectory({ entries: [item(summary("AB-CD", "Glasstaff"))] });
        expect(resolveEntry(directory, "ab-cd")?.name).toBe("Glasstaff");
        expect(resolveEntry(directory, "AB-CD")?.name).toBe("Glasstaff");
        expect(resolveEntry(directory, "zz")).toBeUndefined();
    });

    it("is empty before the list loads, and shared per list object", () => {
        expect(entryDirectory(undefined).items).toEqual([]);
        expect(entryDirectory(list)).toBe(entryDirectory(list));
    });

    it("finds entries by name or alias, ignoring case and spaces around", () => {
        const directory = entryDirectory(list);
        expect(entriesCalled(directory, "  rockseeker ").map((i) => i.entry.id)).toEqual(["a1"]);
        expect(entriesCalled(directory, "GUNDREN ROCKSEEKER").map((i) => i.entry.id)).toEqual(["a1"]);
        expect(entriesCalled(directory, "Gundren")).toEqual([]);
        expect(entriesCalled(directory, " ")).toEqual([]);
    });
});

describe("entry permissions (the API's EntryPermissions)", () => {
    const dm = { memberId: "member-dm", isDm: true };
    const creator = { memberId: CREATOR, isDm: false };
    const player = { memberId: OTHER, isDm: false };

    it("lets a DM, the creator, or anyone edit an Anyone entry", () => {
        const entry = summary("x", "X");
        expect([dm, creator, player].map((v) => canEditEntry(entry, v))).toEqual([true, true, true]);
    });

    it("keeps an Only me entry to its creator and the DMs", () => {
        const entry = summary("x", "X", { editAccess: "OnlyMe" });
        expect([dm, creator, player].map((v) => canEditEntry(entry, v))).toEqual([true, true, false]);
    });

    it("lets only the creator and the DMs change visibility and edit access", () => {
        for (const editAccess of ["Anyone", "OnlyMe"] as const) {
            const entry = summary("x", "X", { editAccess });
            expect([dm, creator, player].map((v) => canChangeEntryAccess(entry, v))).toEqual([true, true, false]);
        }
    });

    it("says Only me to the creator and names the creator to others", () => {
        const entry = summary("x", "X", { editAccess: "OnlyMe" });
        expect(editAccessLabel(entry, CREATOR, "Sam")).toBe("Only me");
        expect(editAccessLabel(entry, OTHER, "Sam")).toBe("Only Sam");
        expect(editAccessLabel(summary("x", "X"), OTHER, "Sam")).toBe("Anyone");
    });
});

describe("the wiki home", () => {
    const ids = (items: EntryListItem[]) => items.map((i) => i.entry.id);

    it("sorts by most mentioned, then most recent mention, then name", () => {
        expect(ids(sortEntries(list.entries, "mentions"))).toEqual(["b2", "a1", "c3", "d4"]);
    });

    it("sorts by the most recent mention, never-mentioned last", () => {
        expect(ids(sortEntries(list.entries, "recent"))).toEqual(["c3", "b2", "a1", "d4"]);
    });

    it("sorts A–Z ignoring case", () => {
        expect(ids(sortEntries(list.entries, "name"))).toEqual(["a1", "c3", "b2", "d4"]);
    });

    it("does not reorder the list it is given", () => {
        const before = ids(list.entries);
        sortEntries(list.entries, "name");
        expect(ids(list.entries)).toEqual(before);
    });

    it("filters by kind and by name or alias", () => {
        expect(ids(filterEntries(list.entries, { kind: "Place", query: "" }))).toEqual(["b2"]);
        expect(ids(filterEntries(list.entries, { kind: null, query: "SEEK" }))).toEqual(["a1"]);
        expect(ids(filterEntries(list.entries, { kind: "Place", query: "seek" }))).toEqual([]);
        expect(ids(filterEntries(list.entries, { kind: null, query: "  " }))).toHaveLength(4);
    });

    it("keeps the sort and kind in the URL, with the defaults as no parameter", () => {
        expect(sortFromQuery("recent")).toBe("recent");
        expect(sortFromQuery(["name"])).toBe("name");
        expect(sortFromQuery("bogus")).toBe("mentions");
        expect(sortFromQuery(undefined)).toBe("mentions");
        expect(sortToQuery("mentions")).toBeUndefined();
        expect(sortToQuery("name")).toBe("name");
        for (const { value } of ENTRY_KINDS) expect(kindFromQuery(kindToQuery(value))).toBe(value);
        expect(kindFromQuery("PLACE")).toBe("Place");
        expect(kindFromQuery("npc")).toBeNull();
        expect(kindToQuery(null)).toBeUndefined();
    });

    it("labels counts and aliases", () => {
        expect([0, 1, 7].map(mentionCountLabel)).toEqual(["No mentions", "1 mention", "7 mentions"]);
        expect(aliasesLabel([])).toBe("");
        expect(aliasesLabel(["Rockseeker", "the dwarf"])).toBe('aka "Rockseeker", "the dwarf"');
    });
});

describe("mentionMarkup", () => {
    it("writes the stored form, which parses back to the name", () => {
        const id = "0b7c5e1a-8f3d-4c2b-9a61-2d4e8f00a001";
        for (const name of ["Gundren", "Gundren [the dwarf]", "*Klarg*_", "a\\b `c` <d> ~e~ & f", "]["]) {
            const markup = mentionMarkup(name, id);
            expect(markup.startsWith("@[")).toBe(true);
            expect(noteMentions(`We met ${markup} today.`)).toEqual([{ entryId: id, text: name }]);
        }
    });
});

describe("aboutPrefill", () => {
    const entry = { id: "0b7c5e1a-8f3d-4c2b-9a61-2d4e8f00a001", name: "Gundren", visibility: "Everyone" as const };
    const mention = "@[Gundren](entry:0b7c5e1a-8f3d-4c2b-9a61-2d4e8f00a001) ";

    it("starts the text with the mention", () => {
        expect(aboutPrefill("", entry)).toEqual({ text: mention, visibility: undefined });
        expect(aboutPrefill("  a draft", entry).text).toBe(`${mention}a draft`);
    });

    it("does not add the mention twice", () => {
        expect(aboutPrefill(`${mention}more`, entry).text).toBe(`${mention}more`);
    });

    it("matches a DM or Me entry's visibility", () => {
        expect(aboutPrefill("", { ...entry, visibility: "DM" }).visibility).toBe("DM");
        expect(aboutPrefill("", { ...entry, visibility: "Me" }).visibility).toBe("Me");
    });
});

describe("addAlias", () => {
    it("trims and appends", () => expect(addAlias(["A"], "  B ", "Name")).toEqual(["A", "B"]));
    it("drops blanks, the name, and repeats ignoring case", () => {
        expect(addAlias(["A"], "  ", "Name")).toEqual(["A"]);
        expect(addAlias(["A"], "name", "Name")).toEqual(["A"]);
        expect(addAlias(["Rock"], "ROCK", "Name")).toEqual(["Rock"]);
    });
    it("stops at the limit", () => {
        const full = Array.from({ length: ENTRY_ALIASES_MAX }, (_, i) => `a${i}`);
        expect(addAlias(full, "more", "Name")).toEqual(full);
    });
});

describe("existingEntryIdFrom", () => {
    it("reads the duplicate-name 409's entry id", () => {
        const error = { response: { status: 409, data: { errors: { name: ["taken"], existingEntryId: ["e1"] } } } };
        expect(existingEntryIdFrom(error)).toBe("e1");
        expect(existingEntryIdFrom({ response: { data: { errors: { name: ["bad"] } } } })).toBeNull();
        expect(existingEntryIdFrom(undefined)).toBeNull();
    });
});
