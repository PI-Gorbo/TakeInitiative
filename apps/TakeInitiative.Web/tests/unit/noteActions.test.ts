import { describe, expect, it } from "vitest";
import type { Session, SessionNote, SessionStreamSession } from "~/utils/api/types";
import { noteActionsFor, noteLink, noteLinkProgress } from "~/utils/noteActions";

const note = (extra: Partial<SessionNote> = {}): SessionNote => ({
    id: "n1",
    sessionId: "s1",
    authorMemberId: "author",
    text: "hi",
    visibility: "Everyone",
    isRecap: false,
    postedAt: "2026-09-20T19:00:00Z",
    images: [],
    addedLater: false,
    editedAt: null,
    isHidden: false,
    hiddenByMemberId: null,
    ...extra,
});

describe("noteActionsFor", () => {
    it("gives the author edit, visibility, copy link and delete", () => {
        expect(noteActionsFor(note(), { isAuthor: true, isDm: false })).toEqual([
            "promote",
            "edit",
            "visibility",
            "copyLink",
            "delete",
        ]);
    });

    it("gives another player only copy link", () => {
        expect(noteActionsFor(note(), { isAuthor: false, isDm: false })).toEqual(["promote", "copyLink"]);
    });

    it("gives a DM hide, or unhide on a hidden note, but never edit or delete of someone else's", () => {
        expect(noteActionsFor(note(), { isAuthor: false, isDm: true })).toEqual(["promote", "hide", "copyLink"]);
        expect(
            noteActionsFor(note({ isHidden: true }), {
                isAuthor: false,
                isDm: true,
            })
        ).toEqual(["promote", "unhide", "copyLink"]);
    });

    it("never offers hide on a Me note", () => {
        expect(
            noteActionsFor(note({ visibility: "Me" }), {
                isAuthor: true,
                isDm: true,
            })
        ).not.toContain("hide");
    });

    it("offers the edit history only once the note was edited", () => {
        expect(
            noteActionsFor(note({ editedAt: "2026-09-21T10:00:00Z" }), {
                isAuthor: false,
                isDm: false,
            })
        ).toEqual(["promote", "history", "copyLink"]);
    });

    it("offers nothing on a note still being posted", () => {
        expect(
            noteActionsFor(note({ id: "pending-abc" }), {
                isAuthor: true,
                isDm: true,
            })
        ).toEqual([]);
    });
});

describe("note links", () => {
    it("links to the Campaign tab with ?note=", () => {
        expect(noteLink("https://ti.app", "c 1", "n1")).toBe("https://ti.app/app/campaigns/c%201?note=n1");
    });

    const loaded = (...numbers: number[]): SessionStreamSession[] =>
        numbers.map((number) => ({
            session: { id: `s${number}`, number } as Session,
            notes: [],
        }));

    it("is done once the note's session is loaded", () => {
        expect(noteLinkProgress(loaded(4, 5), 4, true)).toBe("done");
    });

    it("loads older pages while the session is older than every loaded one", () => {
        expect(noteLinkProgress(loaded(4, 5), 2, true)).toBe("more");
        expect(noteLinkProgress([], 2, true)).toBe("more");
    });

    it("gives up when nothing older is left", () => {
        expect(noteLinkProgress(loaded(4, 5), 2, false)).toBe("missing");
        expect(noteLinkProgress(loaded(1, 2), 7, true)).toBe("missing");
    });
});
