// Editing a note in the composer (step 17). The author's "Edit" brings the note up in
// the composer, with Cancel and Save in place of ➤, so editing reuses the composer's
// text box, `@` mentions, formatting and images instead of a second editor inside
// the card. These are the pure rules behind it; `useComposerEdit` holds which note is
// being edited, and the composer wires the rest to the DOM.
//
// The draft is never touched while editing: the edit has a state of its own, next to
// the composer's, so the draft (and the one saved in localStorage) is simply there
// again when the edit ends, and its images keep uploading meanwhile.
import type { SessionNote, Visibility } from "./api/types";
import { noteHasContent, postableText } from "./composer";
import {
    attachmentsBusy,
    attachmentsFailed,
    attachmentsFromImages,
    imagesChanged,
    readyImages,
    type Attachment,
} from "./images";
import { fromStoredText, mentionBody, toStoredText, type MentionText, type NewEntry } from "./mentions";

/** The note being edited, and its session's number for the "Editing" strip. */
export type NoteEditTarget = { note: SessionNote; sessionNumber: number | null };

/**
 * What an edit changes: the text (mentions as `@[Name]`, 15d), the recap flag and
 * the images (16c). Session and visibility are not here: the PUT cannot change them
 * (visibility has its own action on the note).
 */
export type NoteEditState = MentionText & { isRecap: boolean; attachments: Attachment[] };

export const emptyNoteEditState = (): NoteEditState => ({
    text: "",
    links: {},
    newEntries: [],
    isRecap: false,
    attachments: [],
});

/** The note, as the composer shows it: its images are already on it. */
export const noteEditState = (note: SessionNote): NoteEditState => ({
    ...fromStoredText(note.text),
    newEntries: [],
    isRecap: note.isRecap,
    attachments: attachmentsFromImages(note.images, { onNote: true }),
});

/** Whether Save is on: content within the limit, and no failed upload. */
export function canSaveNoteEdit(edit: NoteEditState): boolean {
    if (attachmentsFailed(edit.attachments)) return false;
    return noteHasContent({
        text: edit.text,
        attachmentCount: edit.attachments.length,
        storedLength: toStoredText(edit.text, edit.links).trim().length,
    });
}

/** The PUT's body. `imageIds` only when the images changed: left out, the API keeps them. */
export type NoteEditBody = { text: string; isRecap: boolean; newEntries?: NewEntry[]; imageIds?: string[] };

/**
 * What Save does:
 * - `blocked`: nothing yet (an upload is still going, one failed, or no content);
 * - `unchanged`: the edit changed nothing, so it just ends, with no request;
 * - `save`: PUT this body.
 */
export type NoteEditPlan = { kind: "blocked" } | { kind: "unchanged" } | { kind: "save"; body: NoteEditBody };

export function noteEditPlan(note: SessionNote, edit: NoteEditState): NoteEditPlan {
    if (attachmentsBusy(edit.attachments) || !canSaveNoteEdit(edit)) return { kind: "blocked" };
    const trimmed = postableText(edit.text);
    // A caption may be empty when the note keeps an image.
    const body = trimmed === null ? { text: "", newEntries: [] } : mentionBody(edit, trimmed);
    const imageIds = imagesChanged(note.images, edit.attachments)
        ? readyImages(edit.attachments).map((i) => i.id)
        : undefined;
    if (body.text === note.text && edit.isRecap === note.isRecap && body.newEntries.length === 0 && !imageIds) {
        return { kind: "unchanged" };
    }
    return {
        kind: "save",
        body: {
            text: body.text,
            isRecap: edit.isRecap,
            ...(body.newEntries.length > 0 ? { newEntries: body.newEntries } : {}),
            ...(imageIds ? { imageIds } : {}),
        },
    };
}

/**
 * Who reads the note, for the reveal warning (15d). A hidden `Everyone` note is read
 * by the DMs and its author only.
 */
export const noteEditAudience = (note: Pick<SessionNote, "isHidden" | "visibility">): Visibility =>
    note.isHidden && note.visibility === "Everyone" ? "DM" : note.visibility;
