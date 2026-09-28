import { describe, expect, it } from "vitest";
import type { SessionNote } from "~/utils/api/types";
import { NOTE_TEXT_MAX, newEntryName, noteHasContent, noteWriteError } from "~/utils/composer";
import { markFailed, moveAttachment, newAttachment, type Attachment } from "~/utils/images";
import {
    canSaveNoteEdit,
    emptyNoteEditState,
    noteEditAudience,
    noteEditPlan,
    noteEditState,
    type NoteEditState,
} from "~/utils/noteEdit";

const GUNDREN = "0b7c5e1a-8f3d-4c2b-9a61-2d4e8f00a0ff";
const NEW_ID = "7d1e2f3a-4b5c-4d6e-8f90-a1b2c3d4e5f6";
const image = (id: string) => ({ id, width: 640, height: 480 });

const note = (extra: Partial<SessionNote> = {}): SessionNote => ({
    id: "n1",
    sessionId: "s1",
    authorMemberId: "author",
    text: `Met @[Gundren](entry:${GUNDREN}) on the road`,
    visibility: "Everyone",
    isRecap: false,
    postedAt: "2026-09-20T19:00:00Z",
    images: [],
    addedLater: false,
    editedAt: null,
    isHidden: false,
    hiddenByMemberId: null,
    ...extra,
});

const ready = (key: string, id: string): Attachment => ({
    ...newAttachment(key, ""),
    status: "ready",
    progress: 1,
    image: image(id),
});

const failure = (status: number, errors: Record<string, string[]>) => ({ response: { status, data: { errors } } });

describe("noteEditState", () => {
    it("reads the note back into the composer's form, with its images on the note", () => {
        const state = noteEditState(note({ isRecap: true, images: [image("i1")] }));
        expect(state.text).toBe("Met @[Gundren] on the road");
        expect(state.links).toEqual({ Gundren: GUNDREN });
        expect(state.newEntries).toEqual([]);
        expect(state.isRecap).toBe(true);
        expect(state.attachments).toHaveLength(1);
        expect(state.attachments[0]).toMatchObject({ status: "ready", onNote: true, image: image("i1") });
    });

    it("starts empty when no note is being edited", () => {
        expect(emptyNoteEditState()).toEqual({ text: "", links: {}, newEntries: [], isRecap: false, attachments: [] });
    });
});

describe("noteEditPlan", () => {
    it("sends nothing when nothing changed", () => {
        const n = note({ images: [image("i1")] });
        expect(noteEditPlan(n, noteEditState(n))).toEqual({ kind: "unchanged" });
    });

    it("sends nothing for whitespace the post would trim anyway", () => {
        const n = note();
        const edit = noteEditState(n);
        expect(noteEditPlan(n, { ...edit, text: `  ${edit.text}\n` })).toEqual({ kind: "unchanged" });
    });

    it("saves changed text in its stored form, keeping the images", () => {
        const n = note({ images: [image("i1")] });
        const edit = noteEditState(n);
        expect(noteEditPlan(n, { ...edit, text: "Met @[Gundren] at the inn" })).toEqual({
            kind: "save",
            body: { text: `Met @[Gundren](entry:${GUNDREN}) at the inn`, isRecap: false },
        });
    });

    it("saves the recap flag alone", () => {
        const n = note();
        const plan = noteEditPlan(n, { ...noteEditState(n), isRecap: true });
        expect(plan).toEqual({ kind: "save", body: { text: n.text, isRecap: true } });
    });

    it("sends the images only when they changed: removed, reordered or added", () => {
        const n = note({ images: [image("i1"), image("i2")] });
        const edit = noteEditState(n);
        const removed = noteEditPlan(n, { ...edit, attachments: edit.attachments.slice(1) });
        expect(removed).toMatchObject({ kind: "save", body: { imageIds: ["i2"] } });
        const moved = noteEditPlan(n, { ...edit, attachments: moveAttachment(edit.attachments, "image-i1", 1) });
        expect(moved).toMatchObject({ kind: "save", body: { imageIds: ["i2", "i1"] } });
        const added = noteEditPlan(n, { ...edit, attachments: [...edit.attachments, ready("new", "i3")] });
        expect(added).toMatchObject({ kind: "save", body: { imageIds: ["i1", "i2", "i3"] } });
    });

    it("lets the caption go empty while the note keeps an image", () => {
        const n = note({ images: [image("i1")] });
        expect(noteEditPlan(n, { ...noteEditState(n), text: "" })).toEqual({
            kind: "save",
            body: { text: "", isRecap: false },
        });
    });

    it("sends a Create's new entry with the text", () => {
        const n = note();
        const edit: NoteEditState = {
            ...noteEditState(n),
            text: "Met @[Gundren] and @[Sildar]",
            links: { Gundren: GUNDREN, Sildar: NEW_ID },
            newEntries: [{ id: NEW_ID, name: "Sildar", kind: "Character" }],
        };
        const plan = noteEditPlan(n, edit);
        expect(plan.kind).toBe("save");
        if (plan.kind !== "save") return;
        expect(plan.body.text).toBe(`Met @[Gundren](entry:${GUNDREN}) and @[Sildar](entry:${NEW_ID})`);
        expect(plan.body.newEntries?.map((e) => e.id)).toEqual([NEW_ID]);
    });

    it("waits while an upload is going, and refuses no content, a failure or too long a text", () => {
        const n = note();
        const edit = noteEditState(n);
        expect(noteEditPlan(n, { ...edit, attachments: [newAttachment("up", "")] })).toEqual({ kind: "blocked" });
        expect(noteEditPlan(n, { ...edit, text: "   " })).toEqual({ kind: "blocked" });
        const failed = markFailed([ready("a", "i9")], "a", "Upload failed");
        expect(noteEditPlan(n, { ...edit, attachments: failed })).toEqual({ kind: "blocked" });
        expect(noteEditPlan(n, { ...edit, text: "x".repeat(NOTE_TEXT_MAX + 1) })).toEqual({ kind: "blocked" });
    });
});

describe("canSaveNoteEdit", () => {
    it("is on for text or images, and off for nothing or a failed upload", () => {
        const edit = noteEditState(note());
        expect(canSaveNoteEdit(edit)).toBe(true);
        expect(canSaveNoteEdit({ ...edit, text: "" })).toBe(false);
        expect(canSaveNoteEdit({ ...edit, text: "", attachments: [ready("a", "i1")] })).toBe(true);
        expect(canSaveNoteEdit({ ...edit, attachments: markFailed([ready("a", "i1")], "a", "x") })).toBe(false);
    });
});

describe("noteEditAudience", () => {
    it("is the note's visibility, except a hidden Everyone note is the DMs'", () => {
        expect(noteEditAudience(note())).toBe("Everyone");
        expect(noteEditAudience(note({ visibility: "Me" }))).toBe("Me");
        expect(noteEditAudience(note({ isHidden: true }))).toBe("DM");
        expect(noteEditAudience(note({ isHidden: true, visibility: "Me" }))).toBe("Me");
    });
});

describe("noteHasContent", () => {
    it("wants text within the limit, or images with an empty caption", () => {
        expect(noteHasContent({ text: "hi", attachmentCount: 0, storedLength: 2 })).toBe(true);
        expect(noteHasContent({ text: " ", attachmentCount: 0, storedLength: 0 })).toBe(false);
        expect(noteHasContent({ text: "", attachmentCount: 1, storedLength: 0 })).toBe(true);
        expect(noteHasContent({ text: "hi", attachmentCount: 0, storedLength: NOTE_TEXT_MAX + 1 })).toBe(false);
    });
});

describe("noteWriteError", () => {
    it("tells a post's and an edit's failures apart", () => {
        expect(noteWriteError(failure(409, { alreadyCreatedEntryId: [NEW_ID] }))).toEqual({ kind: "alreadySaved" });
        expect(noteWriteError(failure(400, { imageIds: ["Gone"] }))).toEqual({ kind: "images", message: "Gone" });
        expect(noteWriteError(failure(409, { existingEntryId: [GUNDREN], newEntryId: [NEW_ID] }))).toEqual({
            kind: "duplicate",
            newEntryId: NEW_ID,
            existingEntryId: GUNDREN,
        });
        expect(noteWriteError(new Error("network"))).toEqual({ kind: "other" });
    });

    it("names the Create a duplicate was about", () => {
        const entries = [{ id: NEW_ID, name: "Sildar", kind: "Character" as const }];
        expect(newEntryName(entries, NEW_ID.toUpperCase())).toBe("Sildar");
        expect(newEntryName(entries, GUNDREN)).toBe("That entry");
    });
});
