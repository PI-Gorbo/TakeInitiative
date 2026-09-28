import { describe, expect, it } from "vitest";
import type { EntryList, EntrySummary, SearchHit, SearchResponse, SearchSection } from "~/utils/api/types";
import { entryDirectory } from "~/utils/entries";
import {
    RECENT_ENTRIES_MAX,
    combatHitLine,
    firstRow,
    hitKey,
    hitTarget,
    isEmptyResponse,
    moveCursor,
    parseSearchInput,
    pushRecent,
    readRecentIds,
    recentEntries,
    recentEntriesKey,
    recentRows,
    rememberRecentEntry,
    replaceSection,
    searchParams,
    searchRows,
    sessionFromQuery,
    sessionLine,
    snippetSegments,
} from "~/utils/search";

// ── Fixtures ─────────────────────────────────────────────────────────────────

const summary = (id: string, name: string, extra: Partial<EntrySummary> = {}): EntrySummary => ({
    id,
    name,
    kind: "Character",
    aliases: [],
    visibility: "Everyone",
    editAccess: "Anyone",
    creatorMemberId: "m1",
    createdAt: "2026-09-01T00:00:00Z",
    updatedAt: "2026-09-01T00:00:00Z",
    mergedFromIds: [],
    ...extra,
});

const entryHit = (id: string, blockId?: string): SearchHit => ({
    kind: "Entry",
    entry: { entry: summary(id, id), mentionCount: 1, matchedOn: blockId ? "Article" : "Name", blockId },
});
const noteHit = (id: string): SearchHit => ({
    kind: "Note",
    note: {
        id,
        sessionId: "s",
        sessionNumber: 3,
        authorMemberId: "m1",
        postedAt: "2026-09-20T19:00:00Z",
        visibility: "Everyone",
        isRecap: false,
        images: [],
        snippet: { text: id, highlights: [] },
    },
});
const sessionHit = (number: number): SearchHit => ({
    kind: "Session",
    session: {
        session: {
            id: `session-${number}`,
            number,
            startedAt: "2026-09-20T19:00:00Z",
            startedByMemberId: "m1",
            isCurrent: false,
        },
    },
});

const combatHit = (id: string, status: "Draft" | "Active" | "Finished" = "Active", matchedCombatant?: string): SearchHit => ({
    kind: "Combat",
    combat: {
        combat: {
            id,
            sessionId: "s",
            name: "Goblin Ambush",
            status,
            round: 3,
            createdAt: "2026-09-20T19:00:00Z",
            combatants: [{ name: "Goblin", entryId: "e1", count: 4 }],
        },
        sessionNumber: 12,
        matchedCombatant,
    },
});

const response = (...sections: SearchSection[]): SearchResponse => ({ query: "q", sections });

// ── parseSearchInput ─────────────────────────────────────────────────────────

describe("parseSearchInput", () => {
    it("reads the prefixes", () => {
        expect(parseSearchInput("gund")).toEqual({ scope: "all", text: "gund" });
        expect(parseSearchInput("@gund")).toEqual({ scope: "entries", text: "gund" });
        expect(parseSearchInput(">start")).toEqual({ scope: "actions", text: "start" });
    });

    it("trims whitespace around the prefix and the text", () => {
        expect(parseSearchInput("   ")).toEqual({ scope: "all", text: "" });
        expect(parseSearchInput("  @  gund ren ")).toEqual({ scope: "entries", text: "gund ren" });
        expect(parseSearchInput(" > wiki")).toEqual({ scope: "actions", text: "wiki" });
        expect(parseSearchInput("@")).toEqual({ scope: "entries", text: "" });
    });

    it("keeps a prefix character that is not first as text", () => {
        expect(parseSearchInput("a@b")).toEqual({ scope: "all", text: "a@b" });
    });

    it("cuts the text at the server's 100 characters", () => {
        expect(parseSearchInput("x".repeat(150)).text).toHaveLength(100);
    });

    it("sends no request for empty text or actions", () => {
        expect(searchParams(parseSearchInput(""))).toBeNull();
        expect(searchParams(parseSearchInput("@"))).toBeNull();
        expect(searchParams(parseSearchInput(">start"))).toBeNull();
        expect(searchParams(parseSearchInput("gund"))).toEqual({ q: "gund" });
        expect(searchParams(parseSearchInput("@gund"))).toEqual({ q: "gund", sections: "entries" });
    });
});

// ── searchRows ───────────────────────────────────────────────────────────────

describe("searchRows", () => {
    it("keeps the server's order, with a header before each section", () => {
        const rows = searchRows(
            response(
                { key: "Entries", hasMore: false, hits: [entryHit("e1")] },
                { key: "Notes", hasMore: false, hits: [noteHit("n1"), noteHit("n2")] },
                { key: "Sessions", hasMore: false, hits: [sessionHit(12)] }
            )
        );
        expect(rows.map((r) => r.type)).toEqual(["header", "hit", "header", "hit", "hit", "header", "hit"]);
        expect(rows.filter((r) => r.type === "header").map((r) => r.type === "header" && r.label)).toEqual([
            "Entries",
            "Notes",
            "Sessions",
        ]);
        expect(new Set(rows.map((r) => r.id)).size).toBe(rows.length);
    });

    it("puts Combats last, under their own header and Show more", () => {
        const rows = searchRows(
            response(
                { key: "Sessions", hasMore: false, hits: [sessionHit(12)] },
                { key: "Combats", hasMore: true, hits: [combatHit("k1"), combatHit("k2", "Finished")] }
            )
        );
        expect(rows.map((r) => r.id)).toEqual([
            "header-Sessions",
            "Sessions-session-session-12",
            "header-Combats",
            "Combats-combat-k1",
            "Combats-combat-k2",
            "more-Combats",
        ]);
        expect(rows.at(-1)).toMatchObject({ type: "more", label: "Show more combats" });
    });

    it("leaves out Reference until 20c draws its rows", () => {
        const reference: SearchSection = {
            key: "Reference",
            hasMore: true,
            hits: [
                {
                    kind: "Reference",
                    reference: {
                        provider: "srd52",
                        providerLabel: "SRD 5.2",
                        id: "goblin-warrior",
                        name: "Goblin Warrior",
                        category: "Monster",
                        detail: "CR 1/4 · Small Fey",
                        url: null,
                        hasStatBlock: true,
                        suggestedKind: "Character",
                    },
                },
            ],
        };
        const rows = searchRows(response({ key: "Entries", hasMore: false, hits: [entryHit("e1")] }, reference));
        expect(rows.map((r) => r.id)).toEqual(["header-Entries", "Entries-entry-e1"]);
        expect(isEmptyResponse(response(reference))).toBe(true);
        expect(hitKey(reference.hits[0]!)).toBe("reference-srd52-goblin-warrior");
    });

    it("leaves out empty sections", () => {
        expect(searchRows(response({ key: "Notes", hasMore: false, hits: [] }))).toEqual([]);
        expect(searchRows(undefined)).toEqual([]);
    });

    it("ends a section with more in Show more, unless it is shown in full", () => {
        const r = response({ key: "Notes", hasMore: true, hits: [noteHit("n1")] });
        const rows = searchRows(r);
        expect(rows.at(-1)).toMatchObject({ type: "more", section: "Notes", label: "Show more notes" });
        expect(searchRows(r, new Set(["Notes"])).some((row) => row.type === "more")).toBe(false);
    });

    it("replaces a section in place", () => {
        const r = response(
            { key: "Entries", hasMore: false, hits: [entryHit("e1")] },
            { key: "Notes", hasMore: true, hits: [noteHit("n1")] },
            { key: "Sessions", hasMore: false, hits: [sessionHit(1)] }
        );
        const longer = replaceSection(r, { key: "Notes", hasMore: false, hits: [noteHit("n1"), noteHit("n2")] });
        expect(longer.sections.map((s) => s.key)).toEqual(["Entries", "Notes", "Sessions"]);
        expect(longer.sections[1].hits).toHaveLength(2);
        expect(r.sections[1].hits).toHaveLength(1);
    });

    it("knows an empty answer", () => {
        expect(isEmptyResponse(response())).toBe(true);
        expect(isEmptyResponse(response({ key: "Notes", hasMore: false, hits: [noteHit("n")] }))).toBe(false);
    });
});

// ── moveCursor ───────────────────────────────────────────────────────────────

describe("moveCursor", () => {
    const rows = [{ type: "header" }, { type: "hit" }, { type: "hit" }, { type: "header" }, { type: "more" }];

    it("skips headers", () => {
        expect(firstRow(rows)).toBe(1);
        expect(moveCursor(rows, 2, 1)).toBe(4);
        expect(moveCursor(rows, 4, -1)).toBe(2);
    });

    it("wraps at the ends", () => {
        expect(moveCursor(rows, 4, 1)).toBe(1);
        expect(moveCursor(rows, 1, -1)).toBe(4);
    });

    it("starts from nowhere at the first or the last row", () => {
        expect(moveCursor(rows, -1, 1)).toBe(1);
        expect(moveCursor(rows, -1, -1)).toBe(4);
    });

    it("has nowhere to go in an empty list, or one of headers only", () => {
        expect(moveCursor([], -1, 1)).toBe(-1);
        expect(moveCursor([{ type: "header" }], 0, 1)).toBe(-1);
        expect(firstRow([])).toBe(-1);
    });
});

// ── Snippet segments ─────────────────────────────────────────────────────────

describe("snippetSegments", () => {
    const marked = (text: string, highlights: { start: number; length: number }[]) =>
        snippetSegments({ text, highlights }).map((s) => (s.mark ? `[${s.text}]` : s.text)).join("");

    it("marks a range at the start and at the end", () => {
        expect(marked("gundren walks", [{ start: 0, length: 7 }])).toBe("[gundren] walks");
        expect(marked("met gundren", [{ start: 4, length: 7 }])).toBe("met [gundren]");
    });

    it("merges ranges next to each other or overlapping", () => {
        expect(
            marked("abcdef", [
                { start: 0, length: 2 },
                { start: 2, length: 2 },
            ])
        ).toBe("[abcd]ef");
        expect(
            marked("abcdef", [
                { start: 3, length: 2 },
                { start: 1, length: 3 },
            ])
        ).toBe("a[bcde]f");
    });

    it("clamps ranges past the end, and drops empty ones", () => {
        expect(marked("abc", [{ start: 1, length: 10 }])).toBe("a[bc]");
        expect(marked("abc", [{ start: 5, length: 2 }])).toBe("abc");
        expect(marked("abc", [{ start: -2, length: 3 }])).toBe("[a]bc");
    });

    it("has no highlights at all", () => {
        expect(snippetSegments({ text: "plain", highlights: [] })).toEqual([{ text: "plain", mark: false }]);
    });
});

// ── hitTarget ────────────────────────────────────────────────────────────────

describe("hitTarget", () => {
    it("opens an entry, at the block for an article hit", () => {
        expect(hitTarget("c1", entryHit("e1"))).toEqual({ path: "/app/campaigns/c1/wiki/e1", query: {} });
        expect(hitTarget("c1", entryHit("e1", "b1"))).toEqual({
            path: "/app/campaigns/c1/wiki/e1",
            query: { block: "b1" },
        });
    });

    it("opens a note in the stream, with no filter", () => {
        expect(hitTarget("c1", noteHit("n1"))).toEqual({ path: "/app/campaigns/c1", query: { note: "n1" } });
    });

    it("opens a session at its divider", () => {
        expect(hitTarget("c1", sessionHit(12))).toEqual({ path: "/app/campaigns/c1", query: { session: "12" } });
    });

    it("opens a combat's page", () => {
        expect(hitTarget("c1", combatHit("k1"))).toEqual({ path: "/app/campaigns/c1/combat/k1", query: {} });
    });

    it("writes a combat's line: live with its round, else its session and status", () => {
        expect(combatHitLine({ status: "Active", round: 3 }, 12)).toBe("Live · Round 3");
        expect(combatHitLine({ status: "Finished", round: 5 }, 12)).toBe("S12 · Finished");
        expect(combatHitLine({ status: "Draft", round: 0 }, 13)).toBe("S13 · Draft");
    });

    it("reads ?session= back", () => {
        expect(sessionFromQuery("12")).toBe(12);
        expect(sessionFromQuery("0")).toBeUndefined();
        expect(sessionFromQuery("s12")).toBeUndefined();
        expect(sessionFromQuery(["12"])).toBeUndefined();
    });

    it("writes a session's line", () => {
        expect(sessionLine(12, "Sat 20 Sep", "The Triboar Trail")).toBe("Session 12 · Sat 20 Sep · The Triboar Trail");
        expect(sessionLine(12, "Sat 20 Sep", null)).toBe("Session 12 · Sat 20 Sep");
    });
});

// ── Recents ──────────────────────────────────────────────────────────────────

class MemoryStorage {
    private items = new Map<string, string>();
    getItem = (key: string) => this.items.get(key) ?? null;
    setItem = (key: string, value: string) => void this.items.set(key, value);
}

describe("recent entries", () => {
    it("puts the latest first, without repeats, at most five", () => {
        let ids: string[] = [];
        for (const id of ["a", "b", "c", "d", "e", "f"]) ids = pushRecent(ids, id);
        expect(ids).toEqual(["f", "e", "d", "c", "b"]);
        expect(pushRecent(ids, "C")).toEqual(["C", "f", "e", "d", "b"]);
        expect(ids).toHaveLength(RECENT_ENTRIES_MAX);
    });

    it("keeps them per campaign in storage", () => {
        const storage = new MemoryStorage() as unknown as Storage;
        rememberRecentEntry(storage, "c1", "a");
        rememberRecentEntry(storage, "c1", "b");
        expect(readRecentIds(storage, "c1")).toEqual(["b", "a"]);
        expect(readRecentIds(storage, "c2")).toEqual([]);
        storage.setItem(recentEntriesKey("c3"), "not json");
        expect(readRecentIds(storage, "c3")).toEqual([]);
        expect(readRecentIds(undefined, "c1")).toEqual([]);
    });

    it("drops hidden and merged entries through the directory", () => {
        const list: EntryList = {
            entries: [
                { entry: summary("a", "Gundren"), mentionCount: 2 },
                { entry: summary("b", "Sildar", { mergedFromIds: ["old"] }), mentionCount: 0 },
            ],
        } as EntryList;
        const items = recentEntries(["hidden", "old", "B", "a"], entryDirectory(list));
        expect(items.map((i) => i.entry.name)).toEqual(["Sildar", "Gundren"]);

        const rows = recentRows(items);
        expect(rows[0]).toMatchObject({ type: "header", label: "Recent entries" });
        expect(hitTarget("c1", (rows[1] as { hit: SearchHit }).hit).path).toBe("/app/campaigns/c1/wiki/b");
        expect(recentRows([])).toEqual([]);
    });
});
