import type { SessionNote } from "~/utils/api/types";
import type { GalleryTile } from "~/utils/gallery";

/**
 * An entry's primary image (SAM-12): the one picture that stands for it on its page, in the
 * wiki list, in search hits and on a combat row.
 *
 * Only an image **everyone** can see may be one. An image carries no visibility of its own —
 * the API serves it to exactly its note's audience — so a 🔒 image as an entry's face would
 * either leak or 404 for half the table. The API enforces this; the picker applies the same
 * rule so an ineligible tile is explained rather than silently missing.
 *
 * All of it is pure, so it is unit tested without a browser.
 */

/**
 * Whether a note's images may stand for an entry: visible to the whole campaign. A `DM` or `Me`
 * note, or a hidden one, is seen by some members and not others. The gallery response already
 * carries both fields, so the picker needs no extra request.
 */
export const canBePrimary = (note: Pick<SessionNote, "visibility" | "isHidden">) =>
    note.visibility === "Everyone" && !note.isHidden;

/** One tile in the picker: the image, and whether it may be chosen. */
export type PrimaryImageChoice = {
    tile: GalleryTile;
    eligible: boolean;
    /** True for the entry's current primary image, so the picker can tick it. */
    current: boolean;
};

/**
 * A gallery's tiles as the picker draws them, in the gallery's own order. Ineligible tiles are
 * kept and marked rather than dropped: "that one is DM-only" is a better answer than a picture
 * that is in the gallery above and missing from the picker below.
 */
export function primaryImageChoices(
    tiles: readonly GalleryTile[],
    primaryImageId: string | null | undefined
): PrimaryImageChoice[] {
    const primary = primaryImageId?.toLowerCase();
    return tiles.map((tile) => ({
        tile,
        eligible: canBePrimary(tile.note),
        current: !!primary && tile.image.id.toLowerCase() === primary,
    }));
}

/** Whether any tile can be chosen, for the picker's empty state. */
export const hasPrimaryImageChoice = (choices: readonly PrimaryImageChoice[]) => choices.some((c) => c.eligible);

export const PRIMARY_IMAGE_MESSAGES = {
    /** The title on an ineligible tile. */
    notEveryone: "Only an image everyone can see can be the primary one.",
    /** No images at all in the entry's gallery. */
    noImages: "No images yet. Attach one to a note that mentions this entry.",
    /** Images, but none the whole table can see. */
    noneEligible: "None of these images can be the primary one: everyone must be able to see it.",
    remove: "Remove primary image",
    failed: "That image could not be made the primary one. Try another.",
} as const;
