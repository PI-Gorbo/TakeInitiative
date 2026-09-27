// Dates and markers on the session stream. Everything is shown in the viewer's
// time zone and locale.

const DAY_MS = 24 * 60 * 60 * 1000;

/**
 * A session divider's date: "Sat 20 Sep", with the year when it is not this year
 * ("Sat 20 Sep 2025"). The parts are ordered the same way in every locale so the
 * divider reads "Session 12 · Sat 20 Sep · Title".
 */
export function formatSessionDate(iso: string, now: Date = new Date(), locale?: string, timeZone?: string): string {
    const date = new Date(iso);
    const format = new Intl.DateTimeFormat(locale, {
        weekday: "short",
        day: "numeric",
        month: "short",
        year: "numeric",
        timeZone,
    });
    const part = (type: Intl.DateTimeFormatPartTypes) =>
        format.formatToParts(date).find((p) => p.type === type)?.value ?? "";
    const thisYear = new Intl.DateTimeFormat(locale, { year: "numeric", timeZone }).format(now);
    const year = part("year");
    return [part("weekday"), part("day"), part("month"), year === thisYear ? "" : year].filter(Boolean).join(" ");
}

/** A note's time of day, e.g. "7:42 pm" or "19:42", by the viewer's locale. */
export function formatNoteTime(iso: string, locale?: string, timeZone?: string): string {
    return new Intl.DateTimeFormat(locale, { hour: "numeric", minute: "2-digit", timeZone }).format(new Date(iso));
}

/** The full date and time, for a tooltip. */
export function formatNoteDateTime(iso: string, locale?: string, timeZone?: string): string {
    return new Intl.DateTimeFormat(locale, { dateStyle: "full", timeStyle: "short", timeZone }).format(new Date(iso));
}

/**
 * The "Added later" marker for a note posted to a session that was no longer
 * current: whole days from the session's start, and "added later" under a day.
 */
export function addedLaterLabel(sessionStartedAt: string, postedAt: string): string {
    const days = Math.floor((Date.parse(postedAt) - Date.parse(sessionStartedAt)) / DAY_MS);
    if (days < 1) return "added later";
    return days === 1 ? "added 1 day later" : `added ${days} days later`;
}
