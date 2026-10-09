import { describe, expect, it } from "vitest";
import type { ArticleBlock } from "~/utils/api/types";
import { builtFromLabel, initialEntryTab, isEntryTab, quoteSourceLabel, quotedNoteIds } from "~/utils/article";

const quote = (noteId: string) => ({
    noteId,
    sessionId: "s",
    sessionNumber: 4,
    authorMemberId: "jo",
    promotedByMemberId: "jo",
    promotedAt: "2026-01-01T00:00:00Z",
});
const block = (id: string, noteId?: string): ArticleBlock => ({
    id,
    text: "text",
    visibility: "Everyone",
    ownerMemberId: "jo",
    quote: noteId ? quote(noteId) : null,
});

describe("initialEntryTab (25d)", () => {
    it("opens the summary when there is one, the notes when it is empty", () => {
        expect(initialEntryTab({ hasSummary: true })).toBe("summary");
        expect(initialEntryTab({ hasSummary: false })).toBe("notes");
    });

    it("follows ?tab= and #timeline", () => {
        expect(initialEntryTab({ hasSummary: true, tab: "notes" })).toBe("notes");
        expect(initialEntryTab({ hasSummary: false, tab: "summary" })).toBe("summary");
        expect(initialEntryTab({ hasSummary: true, hash: "#timeline" })).toBe("notes");
        // ?tab= wins over the hash; an unknown ?tab= is ignored.
        expect(
            initialEntryTab({
                hasSummary: true,
                tab: "summary",
                hash: "#timeline",
            })
        ).toBe("summary");
        expect(initialEntryTab({ hasSummary: true, tab: "gallery" })).toBe("summary");
        expect(initialEntryTab({ hasSummary: true, hash: "#other" })).toBe("summary");
    });

    it("forces the summary for ?edit= and ?block=", () => {
        expect(
            initialEntryTab({
                hasSummary: false,
                forceSummary: true,
                tab: "notes",
                hash: "#timeline",
            })
        ).toBe("summary");
    });

    it("knows its tabs", () => {
        expect(isEntryTab("notes")).toBe(true);
        expect(isEntryTab("summary")).toBe(true);
        expect(isEntryTab("Notes")).toBe(false);
        expect(isEntryTab(undefined)).toBe(false);
    });
});

describe("quotedNoteIds", () => {
    it("collects each quoted note once, lower-cased", () => {
        const ids = quotedNoteIds([block("b1", "N1"), block("b2"), block("b3", "n1"), block("b4", "n2")]);
        expect([...ids]).toEqual(["n1", "n2"]);
        expect(quotedNoteIds(undefined).size).toBe(0);
    });
});

describe("builtFromLabel", () => {
    it("says nothing for a summary written by hand", () => {
        expect(builtFromLabel(0, 12)).toBeNull();
        expect(builtFromLabel(0, null)).toBeNull();
    });

    it("counts the notes quoted of the notes there are", () => {
        expect(builtFromLabel(3, 12)).toBe("Built from 3 of 12 notes");
        expect(builtFromLabel(1, 1)).toBe("Built from 1 of 1 note");
    });

    it("leaves out the total when it is not known or is short", () => {
        expect(builtFromLabel(1, null)).toBe("Built from 1 note");
        expect(builtFromLabel(3, null)).toBe("Built from 3 notes");
        // A quoted note the viewer can no longer see among the notes.
        expect(builtFromLabel(3, 2)).toBe("Built from 3 notes");
    });
});

describe("quoteSourceLabel", () => {
    it("names the author and the session", () => {
        expect(quoteSourceLabel("Jo", false, 4)).toBe("From Jo's note · Session 4");
        expect(quoteSourceLabel("Jo", true, 4)).toBe("From your note · Session 4");
    });
});
