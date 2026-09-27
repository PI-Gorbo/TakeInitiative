import { describe, expect, it } from "vitest";
import { composerPinned, keyboardInset } from "~/utils/keyboardInset";
import { LONG_PRESS_TOLERANCE, canStartLongPress, movedTooFar } from "~/utils/longPress";
import type { SessionStreamSession } from "~/utils/api/types";
import {
    filterEmptyState,
    filterFromQuery,
    filterToQuery,
    STREAM_FILTERS,
    visibleStreamSessions,
} from "~/utils/streamFilters";

describe("keyboardInset", () => {
    it("is the height the visual viewport lost at the bottom", () => {
        // A 390×844 phone with a 336px keyboard.
        expect(keyboardInset({ innerHeight: 844, visual: { height: 508, offsetTop: 0, scale: 1 } })).toBe(336);
    });

    it("counts the visual viewport's offset when iOS pans the page up", () => {
        expect(keyboardInset({ innerHeight: 844, visual: { height: 508, offsetTop: 120, scale: 1 } })).toBe(216);
    });

    it("is 0 with no keyboard, without visualViewport, and while pinch-zoomed", () => {
        expect(keyboardInset({ innerHeight: 844, visual: { height: 844, offsetTop: 0, scale: 1 } })).toBe(0);
        expect(keyboardInset({ innerHeight: 844 })).toBe(0);
        expect(keyboardInset({ innerHeight: 844, visual: { height: 400, offsetTop: 100, scale: 2 } })).toBe(0);
    });

    it("never goes negative, and rounds fractional pixels", () => {
        expect(keyboardInset({ innerHeight: 844, visual: { height: 845.5, offsetTop: 0, scale: 1 } })).toBe(0);
        expect(keyboardInset({ innerHeight: 844, visual: { height: 507.6, offsetTop: 0, scale: 1 } })).toBe(336);
    });
});

describe("composerPinned", () => {
    it("pins while focused on a phone, or wherever a keyboard covers the page", () => {
        expect(composerPinned({ focused: true, phone: true, inset: 0 })).toBe(true);
        expect(composerPinned({ focused: true, phone: false, inset: 300 })).toBe(true);
        expect(composerPinned({ focused: true, phone: false, inset: 0 })).toBe(false);
        expect(composerPinned({ focused: false, phone: true, inset: 300 })).toBe(false);
    });
});

describe("long-press", () => {
    it("is cancelled by movement past the tolerance", () => {
        expect(movedTooFar({ x: 0, y: 0 }, { x: 3, y: 4 })).toBe(false);
        expect(movedTooFar({ x: 0, y: 0 }, { x: 0, y: LONG_PRESS_TOLERANCE + 1 })).toBe(true);
    });

    it("starts only for a primary touch or pen, off controls, when enabled", () => {
        const base = { pointerType: "touch", isPrimary: true, onControl: false, disabled: false };
        expect(canStartLongPress(base)).toBe(true);
        expect(canStartLongPress({ ...base, pointerType: "pen" })).toBe(true);
        expect(canStartLongPress({ ...base, pointerType: "mouse" })).toBe(false);
        expect(canStartLongPress({ ...base, isPrimary: false })).toBe(false);
        expect(canStartLongPress({ ...base, onControl: true })).toBe(false);
        expect(canStartLongPress({ ...base, disabled: true })).toBe(false);
    });
});

describe("stream filters", () => {
    it("reads the lower-case URL value, defaulting to All", () => {
        expect(filterFromQuery("recaps")).toBe("Recaps");
        expect(filterFromQuery("MINE")).toBe("Mine");
        expect(filterFromQuery(["images"])).toBe("Images");
        expect(filterFromQuery(undefined)).toBe("All");
        expect(filterFromQuery("channels")).toBe("All");
    });

    it("writes every filter but All back to the URL, and round-trips", () => {
        expect(filterToQuery("All")).toBeUndefined();
        expect(filterToQuery("Combats")).toBe("combats");
        for (const { value } of STREAM_FILTERS) expect(filterFromQuery(filterToQuery(value))).toBe(value);
    });

    it("points at 🖼 in the Images empty state, and names step 18 in the Combats one", () => {
        expect(filterEmptyState("Images")).toEqual({ title: "No images yet. Attach one with 🖼." });
        expect(filterEmptyState("Combats").detail).toContain("step 18");
        expect(filterEmptyState("All").title).toBe("No session notes yet.");
    });

    it("drops the divider of a session with no matching note, except the current one", () => {
        const entry = (number: number, notes: number, isCurrent = false) =>
            ({
                session: { id: `s${number}`, number, title: null, startedAt: "", startedByMemberId: "m", isCurrent },
                notes: Array.from({ length: notes }, (_, i) => ({ id: `n${number}-${i}` })),
            }) as unknown as SessionStreamSession;
        const sessions = [entry(1, 1), entry(2, 0), entry(3, 0, true)];
        expect(visibleStreamSessions(sessions, "All").map((s) => s.session.number)).toEqual([1, 2, 3]);
        expect(visibleStreamSessions(sessions, "Recaps").map((s) => s.session.number)).toEqual([1, 3]);
    });
});
