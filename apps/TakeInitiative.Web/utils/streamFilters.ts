// The session stream's filters (14e, glossary §1): All · Text · Images · Recaps ·
// Combats · Mine. The filter lives in the URL as a lower-case value (`?filter=recaps`)
// and is sent to the API as its enum name; filtering happens on the server.
import type { SessionStreamFilter, SessionStreamSession } from "./api/types";

export const FILTER_PARAM = "filter";

export const STREAM_FILTERS = [
    { value: "All", label: "All" },
    { value: "Text", label: "Text" },
    { value: "Images", label: "Images" },
    { value: "Recaps", label: "Recaps" },
    { value: "Combats", label: "Combats" },
    { value: "Mine", label: "Authored by me" },
] as const satisfies readonly { value: SessionStreamFilter; label: string }[];

/** `?filter=recaps` → `Recaps`. Missing or unknown values are `All`. */
export function filterFromQuery(value: unknown): SessionStreamFilter {
    const raw = Array.isArray(value) ? value[0] : value;
    if (typeof raw !== "string") return "All";
    const match = STREAM_FILTERS.find((f) => f.value.toLowerCase() === raw.toLowerCase());
    return match?.value ?? "All";
}

/** What the filter button shows, and what its accessible name reads (SAM-30). */
export function filterLabel(filter: SessionStreamFilter): string {
    return (STREAM_FILTERS.find((f) => f.value === filter) ?? STREAM_FILTERS[0]).label;
}

/** `Recaps` → "recaps". `All` is no parameter, so the plain URL stays plain. */
export function filterToQuery(filter: SessionStreamFilter): string | undefined {
    return filter === "All" ? undefined : filter.toLowerCase();
}

/**
 * What an empty stream says. A campaign has no session until a member starts Session 1,
 * which reads differently from a filter that matched nothing. Then the composer below
 * is only its "No sessions yet" call to action (step 17), so the stream does not say it
 * again: it says what will be here and points down.
 */
export function filterEmptyState(filter: SessionStreamFilter, hasSessions: boolean): { title: string; detail?: string } {
    if (!hasSessions) {
        return { title: "Session notes will show here.", detail: "Start your first session below." };
    }
    switch (filter) {
        case "Images":
            return { title: "No images yet. Attach one with 🖼." };
        case "Combats":
            return { title: "No combats in these sessions." };
        case "Text":
            return { title: "No text notes yet." };
        case "Recaps":
            return { title: "No recaps yet.", detail: "Mark a note as a recap with 📜 Recap or /recap." };
        case "Mine":
            return { title: "You have not written any session notes yet." };
        default:
            return { title: "No session notes yet." };
    }
}

/**
 * The sessions the stream draws. Under a filter, a session with no matching note or
 * combat card has no divider, except the current one, so the composer's session is
 * always on screen.
 */
export function visibleStreamSessions<T extends SessionStreamSession>(
    sessions: readonly T[],
    filter: SessionStreamFilter
): T[] {
    return sessions.filter(
        (s) => filter === "All" || s.notes.length > 0 || (s.combats?.length ?? 0) > 0 || s.session.isCurrent
    );
}
