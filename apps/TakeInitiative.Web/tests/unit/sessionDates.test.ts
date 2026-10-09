import { describe, expect, it } from "vitest";
import { addedLaterLabel, formatSessionDate } from "~/utils/sessionDates";

describe("formatSessionDate", () => {
    const now = new Date("2026-09-25T12:00:00Z");

    it("reads weekday, day, month", () => {
        expect(formatSessionDate("2026-09-20T18:00:00Z", now, "en-GB", "UTC")).toMatch(/^Sun 20 Sept?$/);
        expect(formatSessionDate("2026-09-20T18:00:00Z", now, "en-US", "UTC")).toBe("Sun 20 Sep");
    });

    it("uses the viewer's time zone", () => {
        expect(formatSessionDate("2026-09-20T23:30:00Z", now, "en-US", "Australia/Sydney")).toBe("Mon 21 Sep");
    });

    it("adds the year for another year", () => {
        expect(formatSessionDate("2025-09-20T18:00:00Z", now, "en-US", "UTC")).toBe("Sat 20 Sep 2025");
    });
});

describe("addedLaterLabel", () => {
    const start = "2026-09-20T18:00:00Z";

    it("says 'added later' under a day", () => {
        expect(addedLaterLabel(start, "2026-09-21T17:59:00Z")).toBe("added later");
    });

    it("counts whole days", () => {
        expect(addedLaterLabel(start, "2026-09-21T18:00:00Z")).toBe("added 1 day later");
        expect(addedLaterLabel(start, "2026-09-23T20:00:00Z")).toBe("added 3 days later");
    });
});
