// The composer's state and the pure rules behind it (14d). The component only wires
// these to the DOM, so they are unit tested without Nuxt. 14e's `/` commands change
// the same `ComposerState` fields the pickers and the recap toggle do.
import type { Component } from "vue";
import type { Session, SessionNote, SessionStreamSession, Visibility } from "./api/types";
import { emptyMentionText, mentionBody, parseDraft, serializeDraft, type MentionText, type NewEntry } from "./mentions";
import { isPendingNote } from "./sessionStreamCache";

/** The API's limit on a note's text (14a), after trimming. */
export const NOTE_TEXT_MAX = 10_000;
/** Show a character count once the text is this close to the limit. */
export const NOTE_TEXT_WARN_AT = 9_000;
/** The gap prompt's threshold. The API's `SessionGap` owns the rule; this mirrors it. */
export const SESSION_GAP_DAYS = 3;

const DAY_MS = 24 * 60 * 60 * 1000;

/** The visibility choices, for the composer's picker and a note's "Change visibility". Glossary §1: DM is all DMs plus the author. */
export const VISIBILITY_OPTIONS = [
    { value: "Everyone", label: "Everyone", hint: "Every member of the campaign" },
    { value: "DM", label: "🔒 DM", hint: "The DMs and the author" },
    { value: "Me", label: "🔒 Me", hint: "Only the author" },
] as const satisfies readonly { value: Visibility; label: string; hint: string }[];

/**
 * The composer's state. `text` is the text box's, where a mention reads `@[Name]`;
 * `links` and `newEntries` are what those mentions link to (15d, `utils/mentions.ts`).
 */
export type ComposerState = MentionText & {
    /** The picked session, or null for the current session. */
    sessionId: string | null;
    visibility: Visibility;
    isRecap: boolean;
};

export const initialComposerState = (draft: MentionText | string = ""): ComposerState => ({
    ...(typeof draft === "string" ? emptyMentionText(draft) : draft),
    sessionId: null,
    visibility: "Everyone",
    isRecap: false,
});

/**
 * After a post: the text clears, and the session, visibility and recap toggle reset
 * to the current session, `Everyone` and off.
 */
export const resetAfterPost = (): ComposerState => initialComposerState();

/** The text as it will be posted, or null when it cannot be posted yet. */
export function postableText(text: string): string | null {
    const trimmed = text.trim();
    return trimmed.length > 0 && trimmed.length <= NOTE_TEXT_MAX ? trimmed : null;
}

/** The session a post goes to: the picked one when it still exists, else the current one. */
export function targetSession(sessionId: string | null, sessions: readonly Session[]): Session | undefined {
    const current = sessions.find((s) => s.isCurrent);
    if (!sessionId) return current;
    return sessions.find((s) => s.id === sessionId) ?? current;
}

export type PostBody = {
    sessionId?: string;
    text: string;
    visibility: Visibility;
    isRecap: boolean;
    newEntries?: NewEntry[];
};

/**
 * The POST body. A picked session that is the current one is sent as no session, so
 * the post follows the current session if someone starts another meanwhile. The text
 * is the stored form (15d), and `newEntries` are the Creates it still mentions.
 */
export function buildPostBody(state: ComposerState, sessions: readonly Session[]): PostBody | null {
    const trimmed = postableText(state.text);
    if (trimmed === null) return null;
    const body = mentionBody(state, trimmed);
    const text = postableText(body.text);
    if (text === null) return null;
    const target = state.sessionId ? sessions.find((s) => s.id === state.sessionId) : undefined;
    return {
        ...(target && !target.isCurrent ? { sessionId: target.id } : {}),
        text,
        visibility: state.visibility,
        isRecap: state.isRecap,
        ...(body.newEntries.length > 0 ? { newEntries: body.newEntries } : {}),
    };
}


/** The optimistic note shown until the POST answers. */
export function optimisticNote(args: {
    tempId: string;
    body: { text: string; visibility: Visibility; isRecap: boolean };
    session: Session;
    authorMemberId: string;
    now: Date;
}): SessionNote {
    return {
        id: args.tempId,
        sessionId: args.session.id,
        authorMemberId: args.authorMemberId,
        text: args.body.text,
        visibility: args.body.visibility,
        isRecap: args.body.isRecap,
        postedAt: args.now.toISOString(),
        addedLater: !args.session.isCurrent,
        editedAt: null,
        isHidden: false,
        hiddenByMemberId: null,
        images: [],
    };
}

/**
 * One button in `ComposerToolbar`. Steps 15 and 16 add `@`, 📷 and 🖼 as items; 14e's
 * commands flip the same state the recap item does.
 */
export type ComposerToolbarItem = {
    id: string;
    label: string;
    icon?: Component;
    /** Short text drawn next to (or instead of) the icon, e.g. "Recap". */
    text?: string;
    /** A toggle's state; undefined for a plain button. */
    pressed?: boolean;
    disabled?: boolean;
    /** Shown in the tooltip, e.g. "Ctrl+B". */
    shortcut?: string;
    /** Draw a divider before this item. */
    separatorBefore?: boolean;
    run: () => void;
};

// ── Enter ────────────────────────────────────────────────────────────────────

/**
 * What Enter does in the composer and the note editor. On desktop Enter posts and
 * Shift+Enter is a new line; on a touch screen Enter is always a new line and ➤
 * posts. Enter while an IME is composing belongs to the IME.
 */
export function enterAction(
    event: Pick<KeyboardEvent, "key" | "shiftKey" | "isComposing">,
    touch: boolean
): "post" | "newline" | null {
    if (event.key !== "Enter" || event.isComposing) return null;
    if (touch || event.shiftKey) return "newline";
    return "post";
}

// ── Session picker ───────────────────────────────────────────────────────────

export type SessionOption = { session: Session; label: string; addedLater: boolean };

/** The picker's rows: every session, newest first. An older one posts "added later". */
export function sessionOptions(sessions: readonly Session[]): SessionOption[] {
    return [...sessions]
        .sort((a, b) => b.number - a.number)
        .map((session) => ({
            session,
            label: session.title ? `Session ${session.number} · ${session.title}` : `Session ${session.number}`,
            addedLater: !session.isCurrent,
        }));
}

/** The number "Start Session N" and the gap prompt ask for: current + 1. */
export function nextSessionNumber(sessions: readonly Session[]): number {
    const current = sessions.find((s) => s.isCurrent);
    return (current?.number ?? Math.max(0, ...sessions.map((s) => s.number))) + 1;
}

// ── Gap prompt ───────────────────────────────────────────────────────────────

/** The newest `postedAt` among loaded notes, ignoring optimistic ones. */
export function newestNoteAt(sessions: readonly SessionStreamSession[]): string | null {
    let newest: string | null = null;
    for (const entry of sessions) {
        for (const note of entry.notes) {
            if (isPendingNote(note.id)) continue;
            if (newest === null || Date.parse(note.postedAt) > Date.parse(newest)) newest = note.postedAt;
        }
    }
    return newest;
}

/**
 * Whether the gap prompt shows. The server decides (`suggestNextSession`); a loaded
 * note newer than the gap turns it off at once, so a note someone just posted hides
 * the prompt before the next re-read. An empty or failed read shows nothing.
 */
export function showGapPrompt(suggestNextSession: boolean | undefined, newestAt: string | null, now: Date): boolean {
    if (!suggestNextSession) return false;
    if (newestAt === null) return true;
    return now.getTime() - Date.parse(newestAt) > SESSION_GAP_DAYS * DAY_MS;
}

/** "Last note was 5 days ago. Start Session 14?" */
export function gapPromptText(newestAt: string | null, nextNumber: number, now: Date): string {
    const ask = `Start Session ${nextNumber}?`;
    if (newestAt === null) return ask;
    const days = Math.floor((now.getTime() - Date.parse(newestAt)) / DAY_MS);
    if (days < 1) return ask;
    return `Last note was ${days === 1 ? "1 day" : `${days} days`} ago. ${ask}`;
}

// ── Draft ────────────────────────────────────────────────────────────────────

/** A draft per campaign, in `localStorage`. */
export const draftKey = (campaignId: string) => `ti:composerDraft:${campaignId}`;

type DraftStorage = Pick<Storage, "getItem" | "setItem" | "removeItem">;

/**
 * The saved draft: its text, links and new entries (15d). An empty one when there is
 * none or storage is unavailable. A 14d draft, a plain string, still loads.
 */
export function loadDraft(storage: DraftStorage | undefined, campaignId: string): MentionText {
    try {
        return parseDraft(storage?.getItem(draftKey(campaignId)));
    } catch {
        return emptyMentionText();
    }
}

/** Saves the draft; blank text removes it. Storage errors (private mode, quota) are ignored. */
export function saveDraft(storage: DraftStorage | undefined, campaignId: string, draft: MentionText): void {
    try {
        if (draft.text.trim().length === 0) storage?.removeItem(draftKey(campaignId));
        else storage?.setItem(draftKey(campaignId), serializeDraft(draft));
    } catch {
        // A draft is a convenience; losing it is fine.
    }
}

// ── Toolbar formatting ───────────────────────────────────────────────────────

export type TextEdit = { text: string; selectionStart: number; selectionEnd: number };
export type InlineFormat = "bold" | "italic";

const MARKERS: Record<InlineFormat, string> = { bold: "**", italic: "*" };

/**
 * Wraps the selection in a markdown marker, or unwraps it when the marker is already
 * right around it. With no selection it inserts an empty pair with the caret inside.
 */
export function toggleInline(edit: TextEdit, format: InlineFormat): TextEdit {
    const marker = MARKERS[format];
    const { text, selectionStart: start, selectionEnd: end } = edit;
    const before = text.slice(0, start);
    const selected = text.slice(start, end);
    const after = text.slice(end);

    // Italic's `*` must not be half of a bold `**`.
    const wrapped =
        before.endsWith(marker) &&
        after.startsWith(marker) &&
        (format === "bold" || (!before.endsWith("**") && !after.startsWith("**")) || isBoldItalic(before, after));
    if (wrapped) {
        return {
            text: before.slice(0, -marker.length) + selected + after.slice(marker.length),
            selectionStart: start - marker.length,
            selectionEnd: end - marker.length,
        };
    }
    return {
        text: before + marker + selected + marker + after,
        selectionStart: start + marker.length,
        selectionEnd: end + marker.length,
    };
}

// `***x***`: the inner `*` pair is italic inside bold.
const isBoldItalic = (before: string, after: string) => before.endsWith("***") && after.startsWith("***");

/**
 * Turns the lines the selection touches into a bulleted list, or back into plain
 * lines when every one of them is already a list item.
 */
export function toggleList(edit: TextEdit): TextEdit {
    const { text, selectionStart: start, selectionEnd: end } = edit;
    const lineStart = text.lastIndexOf("\n", start - 1) + 1;
    const nextBreak = text.indexOf("\n", end);
    const lineEnd = nextBreak === -1 ? text.length : nextBreak;
    const lines = text.slice(lineStart, lineEnd).split("\n");
    const isItem = (line: string) => /^[-*] /.test(line);
    const unlist = lines.every(isItem);

    let delta = 0;
    let firstDelta = 0;
    const out = lines.map((line, i) => {
        const next = unlist ? line.slice(2) : `- ${line}`;
        if (i === 0) firstDelta = next.length - line.length;
        delta += next.length - line.length;
        return next;
    });
    return {
        text: text.slice(0, lineStart) + out.join("\n") + text.slice(lineEnd),
        selectionStart: Math.max(lineStart, start + firstDelta),
        selectionEnd: Math.max(lineStart, end + delta),
    };
}
