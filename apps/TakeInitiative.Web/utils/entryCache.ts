// Entry cache updates in one place (15c, the 14c `sessionStreamCache` pattern). Pure
// functions over the query data; `utils/queries/entries.ts` applies them to the query
// client for pushes and responses. Every one is idempotent, keyed by entry or note id.
import type { InfiniteData } from "@tanstack/vue-query";
import type { Entry, EntryList, EntrySummary, EntryTimeline, SessionNote } from "./api/types";
import { mentionedEntryIds } from "./markdown";

export type EntryTimelineData = InfiniteData<EntryTimeline, unknown>;

const sameId = (a: string, b: string) => a.toLowerCase() === b.toLowerCase();

/**
 * Adds or replaces an entry in the wiki list. Mention counts are per viewer and never
 * pushed (15b), so a replaced entry keeps its count and a new one starts at 0 until
 * the list is read again.
 */
export function upsertEntrySummary(list: EntryList | undefined, summary: EntrySummary): EntryList | undefined {
    if (!list) return list;
    const index = list.entries.findIndex((item) => sameId(item.entry.id, summary.id));
    if (index === -1) {
        return { ...list, entries: [...list.entries, { entry: summary, mentionCount: 0, lastMentionedAt: null }] };
    }
    const entries = [...list.entries];
    entries[index] = { ...entries[index], entry: summary };
    return { ...list, entries };
}

/** Drops an entry from the wiki list (`entryRemoved`: the viewer can no longer see it). */
export function removeEntry(list: EntryList | undefined, entryId: string): EntryList | undefined {
    if (!list || !list.entries.some((item) => sameId(item.entry.id, entryId))) return list;
    return { ...list, entries: list.entries.filter((item) => !sameId(item.entry.id, entryId)) };
}

/**
 * A pushed summary over a loaded entry. The summary has every field an entry has in
 * 15c; from 15e the article (and in 15g the stats) stay as loaded.
 */
export function mergeEntrySummary(entry: Entry | undefined, summary: EntrySummary): Entry | undefined {
    if (!entry || !sameId(entry.id, summary.id)) return entry;
    return { ...entry, ...summary };
}

/** Whether a loaded timeline holds a note. */
export const timelineHoldsNote = (data: EntryTimelineData | undefined, noteId: string) =>
    !!data?.pages.some((page) => page.items.some((item) => item.note.id === noteId));

/**
 * The entries whose timeline a note change can alter: every entry the note's text
 * mentions (it may have just joined their timelines), plus every loaded timeline that
 * already holds the note (an edit can drop a mention, and a removal drops the note).
 * `note` is the pushed note, or just its id for a removal.
 */
export function timelineTouchedBy(
    note: Pick<SessionNote, "id"> & Partial<Pick<SessionNote, "text">>,
    loaded: Iterable<{ entryId: string; data: EntryTimelineData | undefined }>
): string[] {
    const ids = new Set(note.text ? mentionedEntryIds(note.text) : []);
    for (const { entryId, data } of loaded) {
        if (timelineHoldsNote(data, note.id)) ids.add(entryId.toLowerCase());
    }
    return [...ids];
}

/** A loaded timeline's items, oldest first across its pages (pages are newest first). */
export function timelineItems(data: EntryTimelineData | undefined) {
    return data ? [...data.pages].reverse().flatMap((page) => page.items) : [];
}
