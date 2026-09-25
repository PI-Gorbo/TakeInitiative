import type { InfiniteData } from "@tanstack/vue-query";
import type { Gallery, GalleryItem, NoteImage, SessionNote, SessionStreamFilter } from "~/utils/api/types";
import { mentionedEntryIds } from "~/utils/markdown";

/**
 * Galleries (16d, glossary: Gallery): the images of one session, or the images whose
 * caption mentions an entry. The API answers with image notes; the web flattens them
 * into tiles, so a note with three images is three tiles that share one caption.
 */

/** Gallery notes per page. The server allows 1–60. */
export const GALLERY_PAGE_SIZE = 30;

/** The tiles an entry page's Gallery section shows before "See all (n)". */
export const ENTRY_GALLERY_PREVIEW = 12;

/** Which viewer answers `?image=` when a gallery opened it (`useImageViewer`'s source). */
export const GALLERY_VIEWER_SOURCE = "gallery";

export type GalleryData = InfiniteData<Gallery, unknown>;

/** One image in a grid, with the note (and caption) it came from. */
export type GalleryTile = {
    image: NoteImage;
    note: SessionNote;
    sessionNumber?: number;
};

/**
 * A loaded gallery's notes, newest first. Pages arrive newest first and each is oldest
 * first, so a grid that grows at the end as older pages load.
 */
export function galleryNotes(data: GalleryData | undefined): GalleryItem[] {
    return data ? data.pages.flatMap((page) => [...page.items].reverse()) : [];
}

/** Notes to tiles, one per image, each note's images in their order. */
export function galleryTiles(items: readonly { note: SessionNote; sessionNumber?: number }[]): GalleryTile[] {
    return items.flatMap(({ note, sessionNumber }) =>
        (note.images ?? []).map((image) => ({ image, note, sessionNumber }))
    );
}

/** The number of images a gallery holds for this viewer, from its newest page. */
export const galleryImageCount = (data: GalleryData | undefined) => data?.pages[0]?.imageCount;

/** "1 image", "5 images". */
export const imageCountLabel = (count: number) => `${count} ${count === 1 ? "image" : "images"}`;

/**
 * A divider's "🖼 n": the images on a session's loaded notes. It is only right when all
 * of the session's notes are loaded, which is under the All and Images filters, so it is
 * undefined under the others (and 0 is not shown).
 */
export function dividerImageCount(
    notes: readonly Pick<SessionNote, "images">[],
    filter: SessionStreamFilter
): number | undefined {
    if (filter !== "All" && filter !== "Images") return undefined;
    const count = notes.reduce((sum, note) => sum + (note.images?.length ?? 0), 0);
    return count > 0 ? count : undefined;
}

export const galleryHoldsNote = (data: GalleryData | undefined, noteId: string) =>
    !!data?.pages.some((page) => page.items.some((item) => item.note.id === noteId));

/** A pushed or answered note, or just an id for a removal. */
export type TouchingNote = Pick<SessionNote, "id"> & Partial<Pick<SessionNote, "sessionId" | "text" | "images">>;

/**
 * The loaded galleries a note change can alter (`sessionNoteUpserted`,
 * `sessionNoteRemoved`, and the mutations' own answers). A note with images can have
 * just joined its session's gallery and the galleries of the entries its caption
 * mentions (15c's `timelineTouchedBy`, with the same inputs). Any loaded gallery that
 * already holds the note can lose it: an edit that drops its images or a mention, a
 * visibility change, a hide, or a delete. `resolve` maps a merged entry's id to the
 * entry it was merged into (15g), whose gallery is keyed by its own id.
 */
export function galleriesTouchedBy(
    note: TouchingNote,
    loaded: {
        sessions: Iterable<{ sessionId: string; data: GalleryData | undefined }>;
        entries: Iterable<{ entryId: string; data: GalleryData | undefined }>;
    },
    resolve: (entryId: string) => string | undefined = () => undefined
): { sessionIds: string[]; entryIds: string[] } {
    const hasImages = (note.images?.length ?? 0) > 0;
    const sessionIds = new Set<string>();
    const entryIds = new Set<string>();
    if (hasImages) {
        if (note.sessionId) sessionIds.add(note.sessionId.toLowerCase());
        for (const id of note.text ? mentionedEntryIds(note.text) : []) {
            entryIds.add(id.toLowerCase());
            const target = resolve(id);
            if (target) entryIds.add(target.toLowerCase());
        }
    }
    for (const { sessionId, data } of loaded.sessions) {
        if (galleryHoldsNote(data, note.id)) sessionIds.add(sessionId.toLowerCase());
    }
    for (const { entryId, data } of loaded.entries) {
        if (galleryHoldsNote(data, note.id)) entryIds.add(entryId.toLowerCase());
    }
    return { sessionIds: [...sessionIds], entryIds: [...entryIds] };
}
