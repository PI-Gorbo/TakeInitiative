import { describe, expect, it } from "vitest";
import type { Session, SessionNote, SessionStreamSession } from "~/utils/api/types";
import {
    NOTE_TEXT_MAX,
    buildPostBody,
    draftKey,
    enterAction,
    gapPromptText,
    initialComposerState,
    loadDraft,
    newestNoteAt,
    nextSessionNumber,
    optimisticNote,
    postableText,
    resetAfterPost,
    saveDraft,
    sessionOptions,
    showGapPrompt,
    targetSession,
    toggleInline,
    toggleList,
    type TextEdit,
} from "~/utils/composer";

const session = (number: number, extra: Partial<Session> = {}): Session => ({
    id: `s${number}`,
    number,
    title: null,
    startedAt: `2026-09-${String(number).padStart(2, "0")}T18:00:00Z`,
    startedByMemberId: "m",
    isCurrent: false,
    ...extra,
});
// GET sessions order: newest first.
const sessions = [session(3, { isCurrent: true, title: "Triboar" }), session(2), session(1)];

describe("composer state", () => {
    it("starts on the current session, Everyone, not a recap, with the draft text", () => {
        expect(initialComposerState("draft")).toEqual({
            text: "draft",
            sessionId: null,
            visibility: "Everyone",
            isRecap: false,
        });
    });

    it("resets the session, visibility and recap toggle after a post", () => {
        expect(resetAfterPost()).toEqual(initialComposerState());
    });

    it("trims the text and refuses blank or too long text", () => {
        expect(postableText("  hi \n")).toBe("hi");
        expect(postableText("  \n ")).toBeNull();
        expect(postableText("x".repeat(NOTE_TEXT_MAX))).not.toBeNull();
        expect(postableText("x".repeat(NOTE_TEXT_MAX + 1))).toBeNull();
    });

    it("targets the picked session, or the current one", () => {
        expect(targetSession(null, sessions)?.number).toBe(3);
        expect(targetSession("s1", sessions)?.number).toBe(1);
        expect(targetSession("gone", sessions)?.number).toBe(3);
        expect(targetSession(null, [])).toBeUndefined();
    });

    it("sends no sessionId for the current session and the id for an older one", () => {
        const base = { ...initialComposerState(" **10gp** each "), visibility: "DM" as const, isRecap: true };
        expect(buildPostBody(base, sessions)).toEqual({ text: "**10gp** each", visibility: "DM", isRecap: true });
        expect(buildPostBody({ ...base, sessionId: "s3" }, sessions)).not.toHaveProperty("sessionId");
        expect(buildPostBody({ ...base, sessionId: "s1" }, sessions)).toMatchObject({ sessionId: "s1" });
        expect(buildPostBody({ ...base, text: " " }, sessions)).toBeNull();
    });

    it("builds an optimistic note that is added later when the session is not current", () => {
        const now = new Date("2026-09-25T20:00:00Z");
        const body = { text: "hi", visibility: "Me" as const, isRecap: false };
        const current = optimisticNote({ tempId: "pending-1", body, session: sessions[0], authorMemberId: "me", now });
        expect(current).toMatchObject({
            id: "pending-1",
            sessionId: "s3",
            authorMemberId: "me",
            visibility: "Me",
            postedAt: now.toISOString(),
            addedLater: false,
            isHidden: false,
        });
        const older = optimisticNote({ tempId: "pending-2", body, session: sessions[2], authorMemberId: "me", now });
        expect(older.addedLater).toBe(true);
    });
});

describe("enterAction", () => {
    const key = (k: string, shiftKey = false, isComposing = false) => ({ key: k, shiftKey, isComposing });

    it("posts on Enter and makes a new line on Shift+Enter on desktop", () => {
        expect(enterAction(key("Enter"), false)).toBe("post");
        expect(enterAction(key("Enter", true), false)).toBe("newline");
    });

    it("never posts on Enter on a touch screen", () => {
        expect(enterAction(key("Enter"), true)).toBe("newline");
    });

    it("leaves IME composition and other keys alone", () => {
        expect(enterAction(key("Enter", false, true), false)).toBeNull();
        expect(enterAction(key("a"), false)).toBeNull();
    });
});

describe("session picker", () => {
    it("lists every session newest first, older ones added later", () => {
        const options = sessionOptions([session(1), session(3, { isCurrent: true, title: "Triboar" }), session(2)]);
        expect(options.map((o) => [o.label, o.addedLater])).toEqual([
            ["Session 3 · Triboar", false],
            ["Session 2", true],
            ["Session 1", true],
        ]);
    });

    it("offers current + 1 as the next session", () => {
        expect(nextSessionNumber(sessions)).toBe(4);
        expect(nextSessionNumber([])).toBe(1);
    });
});

describe("gap prompt", () => {
    const now = new Date("2026-09-25T20:00:00Z");
    const note = (id: string, postedAt: string) => ({ id, postedAt }) as SessionNote;
    const loaded = (notes: SessionNote[]): SessionStreamSession[] => [{ session: sessions[0], notes }];

    it("finds the newest loaded note, ignoring optimistic ones", () => {
        const data = loaded([
            note("a", "2026-09-20T19:00:00Z"),
            note("b", "2026-09-19T19:00:00Z"),
            note("pending-x", "2026-09-25T19:59:00Z"),
        ]);
        expect(newestNoteAt(data)).toBe("2026-09-20T19:00:00Z");
        expect(newestNoteAt([])).toBeNull();
    });

    it("shows only when the server suggests it and no loaded note is newer than 3 days", () => {
        expect(showGapPrompt(true, "2026-09-20T19:00:00Z", now)).toBe(true);
        expect(showGapPrompt(true, "2026-09-24T19:00:00Z", now)).toBe(false);
        expect(showGapPrompt(true, null, now)).toBe(true);
        expect(showGapPrompt(false, "2026-09-01T19:00:00Z", now)).toBe(false);
        expect(showGapPrompt(undefined, null, now)).toBe(false);
    });

    it("says how long ago the last note was", () => {
        expect(gapPromptText("2026-09-20T19:00:00Z", 14, now)).toBe("Last note was 5 days ago. Start Session 14?");
        expect(gapPromptText("2026-09-24T19:00:00Z", 14, now)).toBe("Last note was 1 day ago. Start Session 14?");
        expect(gapPromptText(null, 14, now)).toBe("Start Session 14?");
    });
});

describe("draft", () => {
    function memory() {
        const map = new Map<string, string>();
        return {
            map,
            getItem: (k: string) => map.get(k) ?? null,
            setItem: (k: string, v: string) => void map.set(k, v),
            removeItem: (k: string) => void map.delete(k),
        };
    }

    it("keeps one draft per campaign and removes a blank one", () => {
        const storage = memory();
        saveDraft(storage, "c1", "half a thought");
        saveDraft(storage, "c2", "other");
        expect(loadDraft(storage, "c1")).toBe("half a thought");
        expect(storage.map.get(draftKey("c2"))).toBe("other");
        saveDraft(storage, "c1", "  ");
        expect(storage.map.has(draftKey("c1"))).toBe(false);
        expect(loadDraft(storage, "c1")).toBe("");
    });

    it("survives storage that throws or is missing", () => {
        const broken = {
            getItem: () => {
                throw new Error("SecurityError");
            },
            setItem: () => {
                throw new Error("QuotaExceededError");
            },
            removeItem: () => {
                throw new Error("SecurityError");
            },
        };
        expect(loadDraft(broken, "c1")).toBe("");
        expect(() => saveDraft(broken, "c1", "x")).not.toThrow();
        expect(loadDraft(undefined, "c1")).toBe("");
    });
});

describe("toolbar formatting", () => {
    const at = (text: string, selectionStart: number, selectionEnd = selectionStart): TextEdit => ({
        text,
        selectionStart,
        selectionEnd,
    });
    const selected = (e: TextEdit) => e.text.slice(e.selectionStart, e.selectionEnd);

    it("wraps a selection in bold and keeps it selected", () => {
        const next = toggleInline(at("10gp each", 0, 4), "bold");
        expect(next.text).toBe("**10gp** each");
        expect(selected(next)).toBe("10gp");
    });

    it("unwraps bold when the marker is already around the selection", () => {
        const next = toggleInline(at("**10gp** each", 2, 6), "bold");
        expect(next.text).toBe("10gp each");
        expect(selected(next)).toBe("10gp");
    });

    it("inserts an empty pair with the caret inside when nothing is selected", () => {
        const next = toggleInline(at("a ", 2), "italic");
        expect(next.text).toBe("a **");
        expect(next.selectionStart).toBe(3);
    });

    it("italic does not unwrap half of a bold marker, but does unwrap bold italic", () => {
        expect(toggleInline(at("**x**", 2, 3), "italic").text).toBe("***x***");
        expect(toggleInline(at("***x***", 3, 4), "italic").text).toBe("**x**");
        expect(toggleInline(at("*x*", 1, 2), "italic").text).toBe("x");
    });

    it("turns the selected lines into a list and back", () => {
        const listed = toggleList(at("intro\nsword\nshield", 7, 16));
        expect(listed.text).toBe("intro\n- sword\n- shield");
        expect(selected(listed)).toBe("word\n- shie");
        // The caret alone on a list line unlists just that line.
        expect(toggleList(at(listed.text, 9)).text).toBe("intro\nsword\n- shield");
        expect(toggleList(at("", 0))).toEqual({ text: "- ", selectionStart: 2, selectionEnd: 2 });
    });
});
