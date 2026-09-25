import { describe, expect, it } from "vitest";
import type { Entry, EntryList, EntrySummary, EntryTimeline, SessionNote } from "~/utils/api/types";
import {
    mergeEntrySummary,
    removeEntry,
    timelineHoldsNote,
    timelineItems,
    timelineTouchedBy,
    upsertEntrySummary,
    type EntryTimelineData,
} from "~/utils/entryCache";

const GUNDREN = "0b7c5e1a-8f3d-4c2b-9a61-2d4e8f00a001";
const KLARG = "0b7c5e1a-8f3d-4c2b-9a61-2d4e8f00a003";

function summary(id: string, name: string, extra: Partial<EntrySummary> = {}): EntrySummary {
    return {
        id,
        name,
        kind: "Character",
        aliases: [],
        visibility: "Everyone",
        editAccess: "Anyone",
        creatorMemberId: "m1",
        createdAt: "2026-09-01T18:00:00Z",
        updatedAt: "2026-09-01T18:00:00Z",
        ...extra,
    };
}

function note(id: string, minute: number, text = `note ${id}`): SessionNote {
    return {
        id,
        sessionId: "s1",
        authorMemberId: "m1",
        text,
        visibility: "Everyone",
        isRecap: false,
        postedAt: `2026-09-01T19:${String(minute).padStart(2, "0")}:00Z`,
        addedLater: false,
        editedAt: null,
        isHidden: false,
        hiddenByMemberId: null,
    };
}

const page = (notes: SessionNote[], hasOlder: boolean): EntryTimeline => ({
    items: notes.map((n) => ({ note: n, sessionNumber: 1 })),
    hasOlder,
    articleMentions: [],
});
// Newest page first; items oldest first within a page.
const timeline: EntryTimelineData = {
    pages: [page([note("n3", 3), note("n4", 4)], true), page([note("n1", 1), note("n2", 2)], false)],
    pageParams: [undefined, "2026-09-01T19:03:00Z"],
};

describe("the wiki list", () => {
    const list: EntryList = { entries: [{ entry: summary(GUNDREN, "Gundren"), mentionCount: 3, lastMentionedAt: "x" }] };

    it("replaces a pushed entry and keeps the viewer's count", () => {
        const next = upsertEntrySummary(list, summary(GUNDREN.toUpperCase(), "Gundren Rockseeker"));
        expect(next!.entries).toHaveLength(1);
        expect(next!.entries[0]).toMatchObject({ mentionCount: 3, lastMentionedAt: "x" });
        expect(next!.entries[0].entry.name).toBe("Gundren Rockseeker");
    });

    it("adds a new entry with no mentions", () => {
        const next = upsertEntrySummary(list, summary(KLARG, "Klarg"));
        expect(next!.entries.map((i) => [i.entry.name, i.mentionCount])).toEqual([
            ["Gundren", 3],
            ["Klarg", 0],
        ]);
    });

    it("leaves a list that is not loaded alone", () => {
        expect(upsertEntrySummary(undefined, summary(KLARG, "Klarg"))).toBeUndefined();
        expect(removeEntry(undefined, KLARG)).toBeUndefined();
    });

    it("removes an entry, and a repeat is a no-op", () => {
        const next = removeEntry(list, GUNDREN);
        expect(next!.entries).toEqual([]);
        expect(removeEntry(next, GUNDREN)).toBe(next);
    });
});

describe("mergeEntrySummary", () => {
    it("updates the loaded entry with the same id only, and keeps its article", () => {
        const article = { etag: "etag", blocks: [] };
        const entry: Entry = { ...summary(GUNDREN, "Gundren"), article };
        const merged = mergeEntrySummary(entry, summary(GUNDREN, "Renamed"))!;
        expect(merged.name).toBe("Renamed");
        expect(merged.article).toBe(article);
        expect(mergeEntrySummary(entry, summary(KLARG, "Klarg"))).toBe(entry);
        expect(mergeEntrySummary(undefined, summary(KLARG, "Klarg"))).toBeUndefined();
    });
});

describe("timelines", () => {
    it("lists items oldest first across pages", () => {
        expect(timelineItems(timeline).map((i) => i.note.id)).toEqual(["n1", "n2", "n3", "n4"]);
        expect(timelineItems(undefined)).toEqual([]);
    });

    it("knows which notes a loaded timeline holds", () => {
        expect(timelineHoldsNote(timeline, "n2")).toBe(true);
        expect(timelineHoldsNote(timeline, "n9")).toBe(false);
    });

    it("touches the entries a note mentions", () => {
        const pushed = note("n9", 9, `Met @[Gundren](entry:${GUNDREN}) and @[Klarg](entry:${KLARG.toUpperCase()})`);
        expect(timelineTouchedBy(pushed, [])).toEqual([GUNDREN, KLARG]);
    });

    it("touches a loaded timeline that holds the note, even with the mention gone", () => {
        const edited = note("n2", 2, "No mentions any more");
        expect(timelineTouchedBy(edited, [{ entryId: KLARG.toUpperCase(), data: timeline }])).toEqual([KLARG]);
        expect(timelineTouchedBy({ id: "n3" }, [{ entryId: GUNDREN, data: timeline }])).toEqual([GUNDREN]);
        expect(timelineTouchedBy({ id: "n9" }, [{ entryId: GUNDREN, data: timeline }])).toEqual([]);
    });
});
