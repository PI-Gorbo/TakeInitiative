import { describe, expect, it } from "vitest";
import type { EntryList, EntrySummary } from "~/utils/api/types";
import { entryDirectory } from "~/utils/entries";
import { actionRows } from "~/utils/search";
import {
    COMPOSE_TEXT_MAX,
    CREATE_ENTRY_ACTION_ID,
    SEARCH_ACTIONS,
    SEARCH_ACTIONS_MAX,
    composeFits,
    composeFromQuery,
    cycleEntryKind,
    cycleEntryVisibility,
    searchActions,
    type SearchActionContext,
} from "~/utils/searchActions";

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

const list: EntryList = {
    entries: [{ entry: summary("g", "Gundren Rockseeker", { aliases: ["Rockseeker"] }), mentionCount: 3 }],
} as EntryList;

const context = (extra: Partial<SearchActionContext> = {}): SearchActionContext => ({
    campaignId: "c1",
    isDm: false,
    nextSessionNumber: 14,
    scope: "all",
    text: "",
    directory: entryDirectory(list),
    entryHit: null,
    ...extra,
});

const ids = (c: SearchActionContext) => searchActions(c).map((a) => a.id);
const action = (id: string) => SEARCH_ACTIONS.find((a) => a.id === id)!;

// ── Availability ─────────────────────────────────────────────────────────────

describe("searchActions", () => {
    it("offers Start Session N+1 and the tabs for an empty input, with no Create row", () => {
        expect(ids(context())).toEqual(["start-session", "go-campaign", "go-wiki", "go-combat"]);
        expect(action("start-session").label(context())).toBe("Start Session 14");
    });

    it("hides Start Session until the sessions are loaded", () => {
        expect(ids(context())).toContain("start-session");
        expect(ids(context({ nextSessionNumber: null }))).not.toContain("start-session");
        expect(ids(context({ scope: "actions", nextSessionNumber: null }))).not.toContain("start-session");
    });

    it("starts Session 1 in a campaign with none", () => {
        expect(action("start-session").run(context({ nextSessionNumber: 1 }))).toEqual({ kind: "startSession", number: 1 });
    });

    it("offers Create, then New note mentioning, for text", () => {
        expect(ids(context({ text: "Glasstaff" })).slice(0, 2)).toEqual([CREATE_ENTRY_ACTION_ID, "note-mentioning"]);
        expect(action(CREATE_ENTRY_ACTION_ID).label(context({ text: "Glasstaff" }))).toBe("Create entry “Glasstaff”");
    });

    it("hides Create on an exact name or alias, ignoring case, and shows it on a partial one", () => {
        expect(ids(context({ text: "gundren rockseeker" }))).not.toContain(CREATE_ENTRY_ACTION_ID);
        expect(ids(context({ text: "ROCKSEEKER" }))).not.toContain(CREATE_ENTRY_ACTION_ID);
        expect(ids(context({ text: "Gundren" }))).toContain(CREATE_ENTRY_ACTION_ID);
    });

    it("offers Post a note about the entry hit, with the entry in ?about=", () => {
        const c = context({ text: "gund", entryHit: { id: "g", name: "Gundren Rockseeker" } });
        expect(ids(c)).toContain("note-about");
        expect(action("note-about").label(c)).toBe("Post a note about Gundren Rockseeker");
        expect(action("note-about").run(c)).toEqual({
            kind: "navigate",
            target: { path: "/app/campaigns/c1", query: { about: "g" } },
            composer: true,
        });
        expect(ids(context({ text: "gund" }))).not.toContain("note-about");
    });

    it("puts New note mentioning into ?compose=", () => {
        expect(action("note-mentioning").run(context({ text: "Klarg" }))).toEqual({
            kind: "navigate",
            target: { path: "/app/campaigns/c1", query: { compose: "@Klarg" } },
            composer: true,
        });
    });

    it("caps the all scope at four, the query's own first", () => {
        const c = context({ text: "wiki", entryHit: { id: "g", name: "Gundren" } });
        const found = ids(c);
        expect(found).toHaveLength(SEARCH_ACTIONS_MAX);
        expect(found).toEqual([CREATE_ENTRY_ACTION_ID, "note-about", "note-mentioning", "go-wiki"]);
    });

    it("matches the other actions in the all scope", () => {
        expect(ids(context({ text: "Gundren Rockseeker" }))).toEqual(["note-mentioning"]);
        expect(ids(context({ text: "start" }))).toContain("start-session");
    });

    it("offers only the query's own actions in the @ scope", () => {
        expect(ids(context({ scope: "entries", text: "wiki" }))).toEqual([CREATE_ENTRY_ACTION_ID, "note-mentioning"]);
        expect(ids(context({ scope: "entries", text: "" }))).toEqual([]);
    });
});

// ── The > scope ──────────────────────────────────────────────────────────────

describe("searchActions in the > scope", () => {
    it("lists every available action, without the query's own", () => {
        const all = ids(context({ scope: "actions" }));
        expect(all).toEqual(SEARCH_ACTIONS.filter((a) => !a.fromQuery).map((a) => a.id));
        expect(all).not.toContain(CREATE_ENTRY_ACTION_ID);
        expect(all.length).toBeGreaterThan(SEARCH_ACTIONS_MAX);
    });

    it.each(["start", "new session", "s14", "session 14", "START"])("finds Start Session 14 by %s", (text) => {
        expect(ids(context({ scope: "actions", text }))[0]).toBe("start-session");
    });

    it("ranks an exact or prefix match first", () => {
        expect(ids(context({ scope: "actions", text: "wiki" }))[0]).toBe("go-wiki");
        expect(ids(context({ scope: "actions", text: "places" }))).toEqual(["wiki-place"]);
        expect(ids(context({ scope: "actions", text: "recaps" }))).toEqual(["filter-recaps"]);
    });

    it("finds nothing for text no action has", () => {
        expect(ids(context({ scope: "actions", text: "zanthor" }))).toEqual([]);
    });

    it("goes to a tab, a wiki kind and a stream filter", () => {
        const c = context();
        expect(action("go-wiki").run(c)).toEqual({ kind: "navigate", target: { path: "/app/campaigns/c1/wiki", query: {} } });
        expect(action("wiki-faction").run(c)).toEqual({
            kind: "navigate",
            target: { path: "/app/campaigns/c1/wiki", query: { kind: "faction" } },
        });
        expect(action("filter-mine").run(c)).toEqual({
            kind: "navigate",
            target: { path: "/app/campaigns/c1", query: { filter: "mine" } },
        });
    });
});

// ── Rows ─────────────────────────────────────────────────────────────────────

describe("actionRows", () => {
    it("is an Actions header then a row per action, or nothing", () => {
        expect(actionRows([])).toEqual([]);
        const rows = actionRows([{ id: "go-wiki", icon: "→", label: "Go to Wiki" }]);
        expect(rows.map((r) => r.type)).toEqual(["header", "action"]);
        expect(rows[1]).toMatchObject({ id: "action-go-wiki", actionId: "go-wiki", label: "Go to Wiki" });
    });
});

// ── The Create row ───────────────────────────────────────────────────────────

describe("Create row cycling", () => {
    it("cycles the kind with Tab, wrapping", () => {
        expect(cycleEntryKind("Character")).toBe("Place");
        expect(cycleEntryKind("Other")).toBe("Character");
        expect(cycleEntryKind("Character", -1)).toBe("Other");
    });

    it("cycles the visibility with Shift+Tab, wrapping", () => {
        expect(cycleEntryVisibility("Everyone")).toBe("DM");
        expect(cycleEntryVisibility("DM")).toBe("Me");
        expect(cycleEntryVisibility("Me")).toBe("Everyone");
    });
});

// ── ?compose= ────────────────────────────────────────────────────────────────

describe("?compose=", () => {
    it("reads a trimmed value of at most 200 characters", () => {
        expect(composeFromQuery(" @Klarg ")).toBe("@Klarg");
        expect(composeFromQuery(["@a", "@b"])).toBe("@a");
        expect(composeFromQuery("x".repeat(300))).toHaveLength(COMPOSE_TEXT_MAX);
        expect(composeFromQuery("   ")).toBeUndefined();
        expect(composeFromQuery(undefined)).toBeUndefined();
    });

    it("fills an empty composer only", () => {
        expect(composeFits({ text: "", attachmentCount: 0, editing: false })).toBe(true);
        expect(composeFits({ text: "  \n", attachmentCount: 0, editing: false })).toBe(true);
        expect(composeFits({ text: "draft", attachmentCount: 0, editing: false })).toBe(false);
        expect(composeFits({ text: "", attachmentCount: 1, editing: false })).toBe(false);
        expect(composeFits({ text: "", attachmentCount: 0, editing: true })).toBe(false);
    });
});
