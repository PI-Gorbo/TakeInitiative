// Pure updates to the session stream's infinite query data. The hub handlers (14c)
// and the note mutations (14d) both go through these, so a pushed change and the
// caller's own change land in the cache the same way.
//
// Page 0 is the newest page (it ends at the current session); each later page holds
// older sessions. Inside a page, sessions and their notes are oldest first. Every
// function returns new objects for what it changes and the same object when nothing
// changed, and treats a repeated push as a no-op, keyed by note or session id.
import type { InfiniteData } from "@tanstack/vue-query";
import type {
    Session,
    SessionList,
    SessionNote,
    SessionStream,
    SessionStreamFilter,
    SessionStreamSession,
} from "./api/types";

export type SessionStreamData = InfiniteData<SessionStream, unknown>;

// Optimistic notes (14d's composer) carry a temporary id until the POST answers.
// This file has no runtime imports so Node scripts can load it as it is.
const PENDING_PREFIX = "pending-";
export const newPendingNoteId = () =>
    `${PENDING_PREFIX}${Math.random().toString(36).slice(2)}${Date.now().toString(36)}`;
/** Whether a note is the optimistic copy of a post still on its way. */
export const isPendingNote = (noteId: string) => noteId.startsWith(PENDING_PREFIX);

/**
 * Whether a note belongs in a stream loaded with `filter`. The server applies the
 * same rule to reads (14a). In step 14 no note has images or is a combat, so `Text`
 * is every note and `Images` and `Combats` are none.
 */
export function noteMatchesFilter(
    note: Pick<SessionNote, "isRecap" | "authorMemberId">,
    filter: SessionStreamFilter,
    currentMemberId: string | undefined
): boolean {
    switch (filter) {
        case "All":
        case "Text":
            return true;
        case "Recaps":
            return note.isRecap;
        case "Mine":
            return !!currentMemberId && note.authorMemberId === currentMemberId;
        case "Images":
        case "Combats":
            return false;
    }
}

/** Maps every loaded session, keeping the objects of pages nothing changed in. */
function mapSessions(
    data: SessionStreamData,
    fn: (entry: SessionStreamSession) => SessionStreamSession
): SessionStreamData {
    let changed = false;
    const pages = data.pages.map((page) => {
        let pageChanged = false;
        const sessions = page.sessions.map((entry) => {
            const next = fn(entry);
            if (next !== entry) pageChanged = true;
            return next;
        });
        if (!pageChanged) return page;
        changed = true;
        return { ...page, sessions };
    });
    return changed ? { ...data, pages } : data;
}

/** Inserts a note after every note posted at or before it, so `postedAt` order holds. */
function insertByPostedAt(notes: SessionNote[], note: SessionNote): SessionNote[] {
    const at = Date.parse(note.postedAt);
    let index = notes.length;
    while (index > 0 && Date.parse(notes[index - 1].postedAt) > at) index--;
    return [...notes.slice(0, index), note, ...notes.slice(index)];
}

/**
 * Adds or replaces a note, placed in its session by `postedAt`. A no-op when the
 * note's session is not loaded (paging brings it in later). Under a filter the note
 * no longer matches, it is removed instead.
 */
export function upsertNote(
    data: SessionStreamData | undefined,
    note: SessionNote,
    filter: SessionStreamFilter,
    currentMemberId: string | undefined
): SessionStreamData | undefined {
    if (!data) return data;
    if (!noteMatchesFilter(note, filter, currentMemberId)) return removeNote(data, note.id);

    const loaded = data.pages.some((page) => page.sessions.some((s) => s.session.id === note.sessionId));
    if (!loaded) return removeNote(data, note.id);

    return mapSessions(data, (entry) => {
        const index = entry.notes.findIndex((n) => n.id === note.id);
        if (entry.session.id !== note.sessionId) {
            // Notes never move session; this only clears a stray copy.
            return index === -1 ? entry : { ...entry, notes: entry.notes.filter((n) => n.id !== note.id) };
        }
        if (index === -1) return { ...entry, notes: insertByPostedAt(entry.notes, note) };
        if (sameNote(entry.notes[index], note)) return entry;
        const existing = entry.notes[index];
        const notes =
            existing.postedAt === note.postedAt
                ? entry.notes.map((n, i) => (i === index ? note : n))
                : insertByPostedAt(
                      entry.notes.filter((_, i) => i !== index),
                      note
                  );
        return { ...entry, notes };
    });
}

/** Removes a note wherever it is loaded. A no-op when it is not. */
export function removeNote(data: SessionStreamData | undefined, noteId: string): SessionStreamData | undefined {
    if (!data) return data;
    return mapSessions(data, (entry) =>
        entry.notes.some((n) => n.id === noteId)
            ? { ...entry, notes: entry.notes.filter((n) => n.id !== noteId) }
            : entry
    );
}

/**
 * Adds or replaces a session. A loaded session keeps its notes. A new current session
 * newer than every loaded one is appended to the newest page, which becomes its page;
 * the other sessions stop being current, and the gap prompt turns off (an empty
 * current session never suggests another). An older session that is not loaded is
 * ignored: paging brings it in.
 */
export function upsertSession(data: SessionStreamData | undefined, session: Session): SessionStreamData | undefined {
    if (!data || data.pages.length === 0) return data;

    const loaded = data.pages.some((page) => page.sessions.some((s) => s.session.id === session.id));
    let next = data;

    if (session.isCurrent) {
        next = mapSessions(next, (entry) =>
            entry.session.isCurrent && entry.session.id !== session.id
                ? { ...entry, session: { ...entry.session, isCurrent: false } }
                : entry
        );
    }

    if (loaded) {
        next = mapSessions(next, (entry) =>
            entry.session.id === session.id && !sameSession(entry.session, session) ? { ...entry, session } : entry
        );
    } else {
        const newest = Math.max(0, ...next.pages[0].sessions.map((s) => s.session.number));
        if (session.number <= newest) return next;
        const [first, ...rest] = next.pages;
        next = {
            ...next,
            pages: [{ ...first, sessions: [...first.sessions, { session, notes: [] }] }, ...rest],
        };
    }

    if (session.isCurrent && next.pages.some((p) => p.currentSessionId !== session.id)) {
        const becameCurrent = next.pages[0].currentSessionId !== session.id;
        next = {
            ...next,
            pages: next.pages.map((page, i) => ({
                ...page,
                currentSessionId: session.id,
                suggestNextSession: i === 0 && becameCurrent ? false : page.suggestNextSession,
            })),
        };
    }
    return next;
}

/** Every loaded session, oldest first: the order the stream draws them in. */
export function flattenSessions(data: SessionStreamData | undefined): SessionStreamSession[] {
    if (!data) return [];
    const seen = new Set<string>();
    const out: SessionStreamSession[] = [];
    for (let p = data.pages.length - 1; p >= 0; p--) {
        for (const entry of data.pages[p].sessions) {
            if (seen.has(entry.session.id)) continue;
            seen.add(entry.session.id);
            out.push(entry);
        }
    }
    return out.sort((a, b) => a.session.number - b.session.number);
}

function sameNote(a: SessionNote, b: SessionNote): boolean {
    return (
        a.id === b.id &&
        a.sessionId === b.sessionId &&
        a.authorMemberId === b.authorMemberId &&
        a.text === b.text &&
        a.visibility === b.visibility &&
        a.isRecap === b.isRecap &&
        a.postedAt === b.postedAt &&
        a.addedLater === b.addedLater &&
        (a.editedAt ?? null) === (b.editedAt ?? null) &&
        a.isHidden === b.isHidden &&
        (a.hiddenByMemberId ?? null) === (b.hiddenByMemberId ?? null)
    );
}

function sameSession(a: Session, b: Session): boolean {
    return (
        a.id === b.id &&
        a.number === b.number &&
        (a.title ?? null) === (b.title ?? null) &&
        a.startedAt === b.startedAt &&
        a.startedByMemberId === b.startedByMemberId &&
        a.isCurrent === b.isCurrent
    );
}

/**
 * Adds or replaces a session in the `GET sessions` list (newest first) behind the
 * composer's session picker. A new current session takes over `currentSessionId`
 * and turns the gap prompt off, as `upsertSession` does for the stream.
 */
export function upsertSessionInList(data: SessionList | undefined, session: Session): SessionList | undefined {
    if (!data) return data;
    const existing = data.sessions.find((s) => s.id === session.id);
    if (existing && sameSession(existing, session)) return data;

    const others = data.sessions
        .filter((s) => s.id !== session.id)
        .map((s) => (session.isCurrent && s.isCurrent ? { ...s, isCurrent: false } : s));
    const sessions = [...others, session].sort((a, b) => b.number - a.number);
    const becameCurrent = session.isCurrent && data.currentSessionId !== session.id;
    return {
        sessions,
        currentSessionId: session.isCurrent ? session.id : data.currentSessionId,
        suggestNextSession: becameCurrent ? false : data.suggestNextSession,
    };
}

/**
 * Removes the caller's optimistic copy of a note that just arrived by push, before
 * the POST answered: same author, session and text. The response then replaces
 * nothing and upserts a note that is already there.
 */
export function dropPendingCopy(data: SessionStreamData | undefined, note: SessionNote): SessionStreamData | undefined {
    if (!data) return data;
    return mapSessions(data, (entry) => {
        if (entry.session.id !== note.sessionId) return entry;
        const index = entry.notes.findIndex(
            (n) => isPendingNote(n.id) && n.authorMemberId === note.authorMemberId && n.text === note.text
        );
        return index === -1 ? entry : { ...entry, notes: entry.notes.filter((_, i) => i !== index) };
    });
}
