import { describe, expect, it } from "vitest";
import type { EntryChange, NoteImage, SessionNote, Visibility } from "~/utils/api/types";
import { describeChange } from "~/utils/entries";
import { galleryTiles } from "~/utils/gallery";
import { canBePrimary, hasPrimaryImageChoice, primaryImageChoices } from "~/utils/primaryImage";

const PUBLIC_IMAGE = "aaaaaaaa-0000-0000-0000-000000000001";
const DM_IMAGE = "aaaaaaaa-0000-0000-0000-000000000002";
const HIDDEN_IMAGE = "aaaaaaaa-0000-0000-0000-000000000003";
const MINE_IMAGE = "aaaaaaaa-0000-0000-0000-000000000004";

const image = (id: string): NoteImage => ({ id, width: 640, height: 480 });

function note(
    id: string,
    images: NoteImage[],
    { visibility = "Everyone", isHidden = false }: { visibility?: Visibility; isHidden?: boolean } = {}
): SessionNote {
    return {
        id,
        sessionId: "bbbbbbbb-0000-0000-0000-000000000001",
        authorMemberId: "m1",
        text: "Look at @[Vex](entry:cccccccc-0000-0000-0000-000000000001)",
        visibility,
        isRecap: false,
        postedAt: "2026-09-25T20:00:00Z",
        addedLater: false,
        isHidden,
        images,
    } as unknown as SessionNote;
}

const tilesOf = (...notes: SessionNote[]) => galleryTiles(notes.map((n) => ({ note: n, sessionNumber: 1 })));

describe("which images can be primary", () => {
    it("accepts a note the whole campaign can see", () => {
        expect(canBePrimary({ visibility: "Everyone", isHidden: false })).toBe(true);
    });

    it("refuses a DM or Me note, and a hidden one", () => {
        expect(canBePrimary({ visibility: "DM", isHidden: false })).toBe(false);
        expect(canBePrimary({ visibility: "Me", isHidden: false })).toBe(false);
        expect(canBePrimary({ visibility: "Everyone", isHidden: true })).toBe(false);
    });
});

describe("the picker's choices", () => {
    const tiles = tilesOf(
        note("n1", [image(PUBLIC_IMAGE)]),
        note("n2", [image(DM_IMAGE)], { visibility: "DM" }),
        note("n3", [image(HIDDEN_IMAGE)], { isHidden: true }),
        note("n4", [image(MINE_IMAGE)], { visibility: "Me" })
    );

    it("keeps every tile in the gallery's order and marks the ineligible ones", () => {
        const choices = primaryImageChoices(tiles, null);
        expect(choices.map((c) => c.tile.image.id)).toEqual([PUBLIC_IMAGE, DM_IMAGE, HIDDEN_IMAGE, MINE_IMAGE]);
        expect(choices.map((c) => c.eligible)).toEqual([true, false, false, false]);
    });

    it("ticks the current primary image, matching the id's case", () => {
        const choices = primaryImageChoices(tiles, PUBLIC_IMAGE.toUpperCase());
        expect(choices.filter((c) => c.current).map((c) => c.tile.image.id)).toEqual([PUBLIC_IMAGE]);
    });

    it("ticks nothing when the entry has no primary image", () => {
        expect(primaryImageChoices(tiles, null).some((c) => c.current)).toBe(false);
        expect(primaryImageChoices(tiles, undefined).some((c) => c.current)).toBe(false);
    });

    it("splits a note's images into one choice each, sharing the note's eligibility", () => {
        const choices = primaryImageChoices(tilesOf(note("n1", [image(PUBLIC_IMAGE), image(DM_IMAGE)])), null);
        expect(choices).toHaveLength(2);
        expect(choices.every((c) => c.eligible)).toBe(true);
    });

    it("knows when nothing can be chosen", () => {
        expect(hasPrimaryImageChoice(primaryImageChoices(tiles, null))).toBe(true);
        expect(hasPrimaryImageChoice(primaryImageChoices(tiles.slice(1), null))).toBe(false);
        expect(hasPrimaryImageChoice([])).toBe(false);
    });
});

describe("the history lines", () => {
    const line = (change: EntryChange) => describeChange(change, () => "Sam");

    it("names both directions without naming the image", () => {
        expect(line({ type: "PrimaryImageSet", imageId: PUBLIC_IMAGE } as EntryChange)).toBe("set the primary image");
        expect(line({ type: "PrimaryImageCleared" } as EntryChange)).toBe("removed the primary image");
    });
});
