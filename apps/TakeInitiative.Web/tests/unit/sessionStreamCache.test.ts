import { describe, expect, it } from "vitest";
import type { Session, SessionNote, SessionStream } from "~/utils/api/types";
import {
    flattenSessions,
    noteMatchesFilter,
    removeNote,
    upsertNote,
    upsertSession,
    type SessionStreamData,
} from "~/utils/sessionStreamCache";

const ME = "member-me";
const OTHER = "member-other";

function session(number: number, extra: Partial<Session> = {}): Session {
    return {
        id: `s${number}`,
        number,
        title: null,
        startedAt: `2026-09-${String(number).padStart(2, "0")}T18:00:00Z`,
        startedByMemberId: ME,
        isCurrent: false,
        ...extra,
    };
}

function note(id: string, sessionNumber: number, minute: number, extra: Partial<SessionNote> = {}): SessionNote {
    return {
        id,
        sessionId: `s${sessionNumber}`,
        authorMemberId: OTHER,
        text: `note ${id}`,
        visibility: "Everyone",
        isRecap: false,
        postedAt: `2026-09-${String(sessionNumber).padStart(2, "0")}T19:${String(minute).padStart(2, "0")}:00Z`,
        addedLater: false,
        editedAt: null,
        isHidden: false,
        hiddenByMemberId: null,
        ...extra,
    };
}

/** Page 0 holds Sessions 3 and 4 (current); page 1 holds Sessions 1 and 2. */
function stream(): SessionStreamData {
    const page0: SessionStream = {
        sessions: [
            { session: session(3), notes: [note("a", 3, 0), note("b", 3, 10)] },
            { session: session(4, { isCurrent: true }), notes: [note("c", 4, 5)] },
        ],
        currentSessionId: "s4",
        suggestNextSession: true,
        hasOlder: true,
    };
    const page1: SessionStream = {
        sessions: [
            { session: session(1), notes: [] },
            { session: session(2), notes: [note("d", 2, 0)] },
        ],
        currentSessionId: "s4",
        suggestNextSession: true,
        hasOlder: false,
    };
    return { pages: [page0, page1], pageParams: [undefined, 3] };
}

const ids = (data: SessionStreamData | undefined, sessionNumber: number) =>
    flattenSessions(data)
        .find((s) => s.session.number === sessionNumber)!
        .notes.map((n) => n.id);

describe("upsertNote", () => {
    it("appends a new note at the end of its session", () => {
        const next = upsertNote(stream(), note("e", 4, 30), "All", ME);
        expect(ids(next, 4)).toEqual(["c", "e"]);
    });

    it("places a note by postedAt, so a back-posted note lands at the end", () => {
        const data = stream();
        expect(ids(upsertNote(data, note("e", 3, 5), "All", ME), 3)).toEqual(["a", "e", "b"]);
        expect(ids(upsertNote(data, note("f", 3, 59, { addedLater: true }), "All", ME), 3)).toEqual(["a", "b", "f"]);
    });

    it("reaches sessions on older pages", () => {
        expect(ids(upsertNote(stream(), note("e", 2, 30), "All", ME), 2)).toEqual(["d", "e"]);
    });

    it("is idempotent: the same push twice leaves one copy and the same object", () => {
        const once = upsertNote(stream(), note("e", 4, 30), "All", ME);
        const twice = upsertNote(once, note("e", 4, 30), "All", ME);
        expect(ids(twice, 4)).toEqual(["c", "e"]);
        expect(twice).toBe(once);
    });

    it("replaces an existing note in place", () => {
        const edited = note("a", 3, 0, { text: "edited", editedAt: "2026-09-03T20:00:00Z" });
        const next = upsertNote(stream(), edited, "All", ME);
        expect(ids(next, 3)).toEqual(["a", "b"]);
        expect(flattenSessions(next)[2].notes[0].text).toBe("edited");
    });

    it("leaves untouched pages as the same objects", () => {
        const data = stream();
        const next = upsertNote(data, note("e", 4, 30), "All", ME)!;
        expect(next.pages[1]).toBe(data.pages[1]);
        expect(next.pages[0]).not.toBe(data.pages[0]);
    });

    it("is a no-op when the note's session is not loaded", () => {
        const data = stream();
        const orphan = { ...note("e", 9, 0), sessionId: "s-unknown" };
        expect(upsertNote(data, orphan, "All", ME)).toBe(data);
    });

    it("does nothing without data", () => {
        expect(upsertNote(undefined, note("e", 4, 0), "All", ME)).toBeUndefined();
    });

    it("removes a note that no longer matches the filter", () => {
        const recaps: SessionStreamData = stream();
        recaps.pages[0].sessions[0].notes = [note("a", 3, 0, { isRecap: true })];
        const next = upsertNote(recaps, note("a", 3, 0, { isRecap: false }), "Recaps", ME);
        expect(ids(next, 3)).toEqual([]);
    });

    it("adds only matching notes under a filter", () => {
        expect(ids(upsertNote(stream(), note("e", 4, 30), "Mine", ME), 4)).toEqual(["c"]);
        expect(ids(upsertNote(stream(), note("e", 4, 30, { authorMemberId: ME }), "Mine", ME), 4)).toEqual([
            "c",
            "e",
        ]);
        expect(ids(upsertNote(stream(), note("e", 4, 30), "Images", ME), 4)).toEqual(["c"]);
    });
});

describe("removeNote", () => {
    it("removes a note from any page", () => {
        expect(ids(removeNote(stream(), "d"), 2)).toEqual([]);
        expect(ids(removeNote(stream(), "b"), 3)).toEqual(["a"]);
    });

    it("is a no-op for an unknown note", () => {
        const data = stream();
        expect(removeNote(data, "nope")).toBe(data);
    });
});

describe("upsertSession", () => {
    it("appends a newly started session as current and marks the others not current", () => {
        const next = upsertSession(stream(), session(5, { isCurrent: true }))!;
        const all = flattenSessions(next);
        expect(all.map((s) => s.session.number)).toEqual([1, 2, 3, 4, 5]);
        expect(all.filter((s) => s.session.isCurrent).map((s) => s.session.number)).toEqual([5]);
        expect(next.pages.every((p) => p.currentSessionId === "s5")).toBe(true);
        expect(next.pages[0].suggestNextSession).toBe(false);
        expect(ids(next, 5)).toEqual([]);
    });

    it("is idempotent for sessionStarted", () => {
        const once = upsertSession(stream(), session(5, { isCurrent: true }));
        const twice = upsertSession(once, session(5, { isCurrent: true }));
        expect(twice).toBe(once);
        expect(flattenSessions(twice)).toHaveLength(5);
    });

    it("changes a loaded session's title and keeps its notes", () => {
        const next = upsertSession(stream(), session(3, { title: "The Triboar Trail" }));
        const s3 = flattenSessions(next).find((s) => s.session.number === 3)!;
        expect(s3.session.title).toBe("The Triboar Trail");
        expect(s3.notes.map((n) => n.id)).toEqual(["a", "b"]);
    });

    it("ignores an older session that is not loaded", () => {
        const data = stream();
        data.pages.pop();
        expect(upsertSession(data, session(1, { title: "x" }))).toBe(data);
    });
});

describe("flattenSessions", () => {
    it("draws every loaded session oldest first", () => {
        expect(flattenSessions(stream()).map((s) => s.session.number)).toEqual([1, 2, 3, 4]);
    });
});

describe("noteMatchesFilter", () => {
    it("follows the server's filters", () => {
        const recap = note("r", 1, 0, { isRecap: true, authorMemberId: ME });
        const plain = note("p", 1, 0);
        expect(noteMatchesFilter(plain, "All", ME)).toBe(true);
        expect(noteMatchesFilter(plain, "Text", ME)).toBe(true);
        expect(noteMatchesFilter(plain, "Recaps", ME)).toBe(false);
        expect(noteMatchesFilter(recap, "Recaps", ME)).toBe(true);
        expect(noteMatchesFilter(recap, "Mine", ME)).toBe(true);
        expect(noteMatchesFilter(plain, "Mine", ME)).toBe(false);
        expect(noteMatchesFilter(recap, "Mine", undefined)).toBe(false);
        expect(noteMatchesFilter(recap, "Images", ME)).toBe(false);
        expect(noteMatchesFilter(recap, "Combats", ME)).toBe(false);
    });
});
