// Which actions a note offers, and the note deep link (14d). The desktop menu
// (`NoteActions`) and 14e's long-press sheet (`NoteActionSheet`) both draw this
// list and emit the chosen `NoteAction`; `SessionNoteCard` carries it out.
import type { SessionNote, SessionStreamSession } from "./api/types";
import { isPendingNote } from "./sessionStreamCache";

export type NoteAction = "promote" | "edit" | "visibility" | "hide" | "unhide" | "history" | "copyLink" | "delete";

export type NoteActionContext = {
    /** The viewer wrote the note. Only the author edits, changes visibility and deletes (invariant 4). */
    isAuthor: boolean;
    /** The viewer is a DM. DMs hide and unhide. */
    isDm: boolean;
};

/**
 * The actions for a note, in menu order (design §3a's sheet: Promote to wiki, Edit,
 * Hide, Copy link). A note still being posted has none. Promote is for anyone who can
 * see the note (15f); the entry picker then offers only entries they can edit. A DM
 * never hides a `Me` note: only its author can see it. A note with no text (an image
 * note with no caption, 16c) offers no Promote.
 */
export function noteActionsFor(note: SessionNote, { isAuthor, isDm }: NoteActionContext): NoteAction[] {
    if (isPendingNote(note.id)) return [];
    // Promote copies text only, so an image note with no caption has nothing to promote (16b).
    const actions: NoteAction[] = note.text.trim() ? ["promote"] : [];
    if (isAuthor) actions.push("edit", "visibility");
    if (isDm && note.visibility !== "Me") actions.push(note.isHidden ? "unhide" : "hide");
    if (note.editedAt) actions.push("history");
    actions.push("copyLink");
    if (isAuthor) actions.push("delete");
    return actions;
}

export const NOTE_ACTION_LABELS: Record<NoteAction, string> = {
    promote: "Promote to wiki",
    edit: "Edit",
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
