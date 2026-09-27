import { describe, expect, it } from "vitest";
import type { Gallery, NoteImage, SessionNote } from "~/utils/api/types";
import {
    dividerImageCount,
    galleriesTouchedBy,
    galleryImageCount,
    galleryNotes,
    galleryTiles,
    imageCountLabel,
    type GalleryData,
} from "~/utils/gallery";

const HIDEOUT = "11111111-1111-1111-1111-111111111111";
const CASTLE = "22222222-2222-2222-2222-222222222222";
const MERGED = "33333333-3333-3333-3333-333333333333";
const S1 = "aaaaaaaa-0000-0000-0000-000000000001";
const S2 = "aaaaaaaa-0000-0000-0000-000000000002";

const image = (id: string): NoteImage => ({ id, width: 640, height: 480 });
const mention = (name: string, id: string) => `@[${name}](entry:${id})`;

function note(id: string, minute: number, images: NoteImage[], text = "", sessionId = S1): SessionNote {
    return {
        id,
        sessionId,
        authorMemberId: "m1",
        text,
        visibility: "Everyone",
        isRecap: false,
        postedAt: `2026-09-25T20:${String(minute).padStart(2, "0")}:00Z`,
        addedLater: false,
        isHidden: false,
        mentionedEntryIds: [],
        images,
    } as unknown as SessionNote;
}

const page = (notes: SessionNote[], hasOlder: boolean, imageCount: number): Gallery => ({
    items: notes.map((n) => ({ note: n, sessionNumber: 1 })),
    hasOlder,
    imageCount,
});
const data = (...pages: Gallery[]): GalleryData => ({ pages, pageParams: pages.map(() => undefined) });

describe("flattening notes to tiles", () => {
    it("orders notes newest first across pages that are each oldest first", () => {
        const loaded = data(
            page([note("n3", 3, [image("c")]), note("n4", 4, [image("d")])], true, 4),
            page([note("n1", 1, [image("a")]), note("n2", 2, [image("b")])], false, 4)
        );
        expect(galleryNotes(loaded).map((i) => i.note.id)).toEqual(["n4", "n3", "n2", "n1"]);
        expect(galleryNotes(undefined)).toEqual([]);
    });

    it("makes one tile per image, sharing the note's caption, in the note's order", () => {
        const three = note("n1", 1, [image("a"), image("b"), image("c")], "Map of the caves");
        const tiles = galleryTiles([{ note: three, sessionNumber: 2 }, { note: note("n2", 2, [image("d")]) }]);
        expect(tiles.map((t) => t.image.id)).toEqual(["a", "b", "c", "d"]);
        expect(tiles.slice(0, 3).every((t) => t.note.text === "Map of the caves" && t.sessionNumber === 2)).toBe(true);
    });

    it("skips a note with no images", () => {
        expect(galleryTiles([{ note: note("n1", 1, []) }])).toEqual([]);
    });

    it("reads the whole count from the newest page", () => {
        expect(galleryImageCount(data(page([], true, 42), page([], false, 42)))).toBe(42);
        expect(galleryImageCount(undefined)).toBeUndefined();
        expect(imageCountLabel(1)).toBe("1 image");
        expect(imageCountLabel(5)).toBe("5 images");
    });
});

describe("the divider count", () => {
    const notes = [note("n1", 1, [image("a"), image("b")]), note("n2", 2, []), note("n3", 3, [image("c")])];

    it("counts the images on the loaded notes under All and Images", () => {
        expect(dividerImageCount(notes, "All")).toBe(3);
        expect(dividerImageCount(notes, "Images")).toBe(3);
    });

    it("is not shown under the filters that load only some notes", () => {
        for (const filter of ["Text", "Recaps", "Mine", "Combats"] as const) {
            expect(dividerImageCount(notes, filter)).toBeUndefined();
        }
    });

    it("is not shown when there are no images", () => {
        expect(dividerImageCount([note("n1", 1, [])], "All")).toBeUndefined();
        expect(dividerImageCount([], "All")).toBeUndefined();
    });
});

describe("which galleries a pushed note invalidates", () => {
    const none = { sessions: [], entries: [] };

    it("a new image note: its session and the entries its caption mentions", () => {
        const pushed = note("n1", 1, [image("a")], `Map of ${mention("Hideout", HIDEOUT)} and ${mention("Castle", CASTLE)}`);
        expect(galleriesTouchedBy(pushed, none)).toEqual({ sessionIds: [S1], entryIds: [HIDEOUT, CASTLE] });
    });

    it("a text note touches no gallery it is not already in", () => {
        const pushed = note("n1", 1, [], `About ${mention("Hideout", HIDEOUT)}`);
        expect(galleriesTouchedBy(pushed, none)).toEqual({ sessionIds: [], entryIds: [] });
    });

    it("a note that had images, or a mention, leaves the galleries that hold it", () => {
        const held = note("n1", 1, [image("a")], mention("Hideout", HIDEOUT));
        const loaded = {
            sessions: [
                { sessionId: S1, data: data(page([held], false, 1)) },
                { sessionId: S2, data: data(page([note("n9", 9, [image("z")], "", S2)], false, 1)) },
            ],
            entries: [
                { entryId: HIDEOUT, data: data(page([held], false, 1)) },
                { entryId: CASTLE, data: undefined },
            ],
        };
        // Edited to no images and no mention.
        expect(galleriesTouchedBy(note("n1", 1, [], "Just words"), loaded)).toEqual({
            sessionIds: [S1],
            entryIds: [HIDEOUT],
        });
        // Removed: the push has only the id (and the session).
        expect(galleriesTouchedBy({ id: "n1", sessionId: S1 }, loaded)).toEqual({
            sessionIds: [S1],
            entryIds: [HIDEOUT],
        });
        // A caption edited from the hideout to the castle touches both.
        expect(galleriesTouchedBy(note("n1", 1, [image("a")], mention("Castle", CASTLE)), loaded)).toEqual({
            sessionIds: [S1],
            entryIds: [CASTLE, HIDEOUT],
        });
    });

    it("a mention of a merged entry touches the entry it was merged into", () => {
        const pushed = note("n1", 1, [image("a")], mention("Old name", MERGED));
        const resolve = (id: string) => (id === MERGED ? HIDEOUT : undefined);
        expect(galleriesTouchedBy(pushed, none, resolve).entryIds).toEqual([MERGED, HIDEOUT]);
    });
});
