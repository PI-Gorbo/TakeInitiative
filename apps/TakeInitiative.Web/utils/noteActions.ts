// Which actions a note offers, and the note deep link (14d). The desktop menu
// (`NoteActions`) and 14e's long-press sheet (`NoteActionSheet`) both draw this
// list and emit the chosen `NoteAction`; `SessionNoteCard` carries it out.
import type { SessionNote, SessionStreamSession } from "./api/types";
import { isPendingNote } from "./sessionStreamCache";

export type NoteAction = "promote" | "edit" | "suggest" | "visibility" | "hide" | "unhide" | "history" | "copyLink" | "delete";

export type NoteActionContext = {
    /** The viewer wrote the note. Only the author edits, changes visibility and deletes (invariant 4). */
    isAuthor: boolean;
    /** The viewer is a DM. DMs hide and unhide. */
    isDm: boolean;
    /**
     * ✨ may be offered on this note (23f): the suggestion model is not Off on this device, and
     * this is the stream rather than an entry's timeline. Only the author ever sees it, because
     * only the author can accept a suggestion (invariant 4).
     */
    canSuggest?: boolean;
};

/**
 * The actions for a note, in menu order (design §3a's sheet: Promote to wiki, Edit,
 * Hide, Copy link). A note still being posted has none. Promote is for anyone who can
 * see the note (15f); the entry picker then offers only entries they can edit. A DM
 * never hides a `Me` note: only its author can see it. A note with no text (an image
 * note with no caption, 16c) offers no Promote, and nothing for the model to read either (23f).
 */
export function noteActionsFor(note: SessionNote, { isAuthor, isDm, canSuggest = false }: NoteActionContext): NoteAction[] {
    if (isPendingNote(note.id)) return [];
    // Promote copies text only, so an image note with no caption has nothing to promote (16b).
    const actions: NoteAction[] = note.text.trim() ? ["promote"] : [];
    if (isAuthor) {
        actions.push("edit");
        if (canSuggest && note.text.trim()) actions.push("suggest");
        actions.push("visibility");
    }
    if (isDm && note.visibility !== "Me") actions.push(note.isHidden ? "unhide" : "hide");
    if (note.editedAt) actions.push("history");
    actions.push("copyLink");
    if (isAuthor) actions.push("delete");
    return actions;
}

export const NOTE_ACTION_LABELS: Record<NoteAction, string> = {
    promote: "Add to summary",
    edit: "Edit",
    suggest: "Find suggestions",
    visibility: "Change visibility",
    hide: "Hide",
    unhide: "Unhide",
    history: "Edit history",
    copyLink: "Copy link",
    delete: "Delete",
};

/** The query parameter a note link uses: `/app/campaigns/{id}?note={noteId}`. */
export const NOTE_LINK_PARAM = "note";

/** A link that opens the Campaign tab at a note. */
export function noteLink(origin: string, campaignId: string, noteId: string): string {
    return `${origin}/app/campaigns/${encodeURIComponent(campaignId)}?${NOTE_LINK_PARAM}=${encodeURIComponent(noteId)}`;
}

/**
 * Following a note link: whether the stream must load older pages before the note's
 * session is on screen. "done" when the session is loaded (the note may still be
 * filtered out), "more" when an older page can bring it in, "missing" otherwise.
 */
export function noteLinkProgress(
    sessions: readonly SessionStreamSession[],
    sessionNumber: number,
    hasOlder: boolean
): "done" | "more" | "missing" {
    if (sessions.some((s) => s.session.number === sessionNumber)) return "done";
    const oldest = Math.min(...sessions.map((s) => s.session.number));
    return hasOlder && (sessions.length === 0 || oldest > sessionNumber) ? "more" : "missing";
}
