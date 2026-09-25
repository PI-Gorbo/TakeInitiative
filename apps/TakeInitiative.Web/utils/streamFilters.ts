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
    { value: "Mine", label: "Mine" },
] as const satisfies readonly { value: SessionStreamFilter; label: string }[];

/** `?filter=recaps` → `Recaps`. Missing or unknown values are `All`. */
export function filterFromQuery(value: unknown): SessionStreamFilter {
    const raw = Array.isArray(value) ? value[0] : value;
    if (typeof raw !== "string") return "All";
    const match = STREAM_FILTERS.find((f) => f.value.toLowerCase() === raw.toLowerCase());
    return match?.value ?? "All";
}

/** `Recaps` → "recaps". `All` is no parameter, so the plain URL stays plain. */
export function filterToQuery(filter: SessionStreamFilter): string | undefined {
    return filter === "All" ? undefined : filter.toLowerCase();
}

/**
 * What an empty stream says under a filter. Images and Combats have nothing to show
 * until images (step 16) and combats (step 18) join the stream.
 */
export function filterEmptyState(filter: SessionStreamFilter): { title: string; detail?: string } {
    switch (filter) {
        case "Images":
            return {
                title: "No images yet.",
                detail: "Posting images to a session arrives in step 16. They will show here.",
            };
        case "Combats":
            return {
                title: "No combats yet.",
                detail: "Combats join the session stream in step 18. They will show here.",
            };
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
 * The sessions the stream draws. Under a filter, a session with no matching note has
 * no divider, except the current one, so the composer's session is always on screen.
 */
export function visibleStreamSessions<T extends SessionStreamSession>(
    sessions: readonly T[],
    filter: SessionStreamFilter
): T[] {
    return sessions.filter((s) => filter === "All" || s.notes.length > 0 || s.session.isCurrent);
}
