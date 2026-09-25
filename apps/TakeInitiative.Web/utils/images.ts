// Images on session notes (16c): the limits, the image URL, what `prepareImage` does
// with a file, the composer's attachments, the caption nudge and the note layout. All
// of it is pure, so it is unit tested without a browser; `useImageAttachments` wires
// it to uploads and `components/Image/*` draw it.
import type { NoteImage, SessionNote } from "./api/types";
import { mentionedEntryIds, notePlainText } from "./markdown";

/** The API's limit on a note's images (16b). */
export const NOTE_IMAGES_MAX = 10;
/**
 * The API caps an upload at 20 MB (16a). Kestrel can reset the connection on a bigger
 * body before the browser reads the 413, so the client checks first.
 */
export const IMAGE_UPLOAD_MAX_BYTES = 20 * 1024 * 1024;
/** A file with a longer edge than this is redrawn smaller before it goes up. */
export const IMAGE_AS_IS_MAX_EDGE = 8000;
/** The long edge a redrawn image gets at most. */
export const IMAGE_REDRAW_MAX_EDGE = 4096;
/** A redrawn image is a JPEG at this quality. */
export const IMAGE_REDRAW_QUALITY = 0.9;
/** The types the API accepts as they are (16a reads the type from the bytes). */
export const UPLOAD_TYPES: readonly string[] = [
    "image/jpeg",
    "image/png",
    "image/webp",
    "image/gif",
];

export const IMAGE_MESSAGES = {
    /** The API's 415 wording. */
    unsupported: "This image type is not supported. Try JPEG or PNG.",
    tooMany: `A note can have at most ${NOTE_IMAGES_MAX} images.`,
    tooLarge: "This image is too large. The limit is 20 MB.",
    uploadFailed: "The upload failed. Tap to try again.",
    captionNudge:
        "Post without a caption? Images without one are hard to find later.",
    captionPlaceholder: "Add a caption. Who or what is in this? @ to link",
    willBeDeleted: "The image will be deleted.",
    tagHint: "Tag what's in this?",
} as const;

export type ImageVariant = "display" | "thumb";

/**
 * `GET /api/campaigns/{cid}/images/{id}/{variant}`. A plain `<img>` loads it, and the
 * session cookie goes with it because the web and the API are same-site (Notes).
 */
export function imageUrl(
    apiBase: string | undefined,
    campaignId: string,
    imageId: string,
    variant: ImageVariant
): string {
    const base = (apiBase ?? "").replace(/\/+$/, "");
    return `${base}/api/campaigns/${encodeURIComponent(campaignId)}/images/${encodeURIComponent(imageId)}/${variant}`;
}

// ── Preparing a file ─────────────────────────────────────────────────────────

export type ImageSize = { width: number; height: number };

export type PrepareDecision =
    | { action: "upload" }
    | { action: "redraw"; width: number; height: number }
    | { action: "fail"; error: string };

/** A size scaled so its long edge is at most `maxEdge`; never upscaled. */
export function redrawSize(
    width: number,
    height: number,
    maxEdge: number = IMAGE_REDRAW_MAX_EDGE
): ImageSize {
    const long = Math.max(width, height);
    if (long <= maxEdge) return { width, height };
    const scale = maxEdge / long;
    return {
        width: Math.max(1, Math.round(width * scale)),
        height: Math.max(1, Math.round(height * scale)),
    };
}

/**
 * What happens to a file (16c step 2). It goes up unchanged when it is JPEG, PNG, WebP
 * or GIF, at most 20 MB, with a long edge of at most 8000 px. Otherwise, when the
 * browser could decode it (`decoded`), it is redrawn at most 4096 px on its long edge.
 * A file the browser cannot decode goes up only when it is an accepted type within the
 * size, so the API has the last word; anything else fails without an upload.
 */
export function prepareDecision(
    file: { type: string; size: number },
    decoded: ImageSize | null
): PrepareDecision {
    const accepted = UPLOAD_TYPES.includes(file.type.toLowerCase());
    const small = file.size <= IMAGE_UPLOAD_MAX_BYTES;
    if (!decoded) {
        if (accepted && small) return { action: "upload" };
        return {
            action: "fail",
            error: accepted
                ? IMAGE_MESSAGES.tooLarge
                : IMAGE_MESSAGES.unsupported,
        };
    }
    if (
        accepted &&
        small &&
        Math.max(decoded.width, decoded.height) <= IMAGE_AS_IS_MAX_EDGE
    )
        return { action: "upload" };
    return { action: "redraw", ...redrawSize(decoded.width, decoded.height) };
}

export type DecodedImage = ImageSize & { close?: () => void };

/** The browser's part of preparing: decoding and redrawing. Tests pass fakes. */
export type PrepareDeps = {
    /** `createImageBitmap`, or null when the browser cannot decode the file. */
    decode: (file: Blob) => Promise<DecodedImage | null>;
    /** Draws the image at the size and encodes a JPEG, or null when that fails. */
    encode: (image: DecodedImage, size: ImageSize) => Promise<Blob | null>;
};

export type PreparedImage =
    { ok: true; file: Blob; fileName: string } | { ok: false; error: string };

/** The file that goes up, or why nothing does. Always checks the 20 MB limit last. */
export async function prepareImage(
    file: File,
    deps: PrepareDeps
): Promise<PreparedImage> {
    let decoded: DecodedImage | null = null;
    try {
        decoded = await deps.decode(file).catch(() => null);
        const decision = prepareDecision(file, decoded);
        if (decision.action === "fail")
            return { ok: false, error: decision.error };
        if (decision.action === "upload")
            return { ok: true, file, fileName: file.name || "image" };
        const blob = decoded
            ? await deps.encode(decoded, decision).catch(() => null)
            : null;
        if (!blob) return { ok: false, error: IMAGE_MESSAGES.unsupported };
        if (blob.size > IMAGE_UPLOAD_MAX_BYTES)
            return { ok: false, error: IMAGE_MESSAGES.tooLarge };
        return { ok: true, file: blob, fileName: jpegName(file.name) };
    } finally {
        decoded?.close?.();
    }
}

const jpegName = (name: string) =>
    `${(name || "image").replace(/\.[^.]*$/, "") || "image"}.jpg`;

/** The files among a paste or a drop that are images. */
export const imageFiles = (
    files: Iterable<File> | ArrayLike<File> | null | undefined
): File[] =>
    files
        ? Array.from(files).filter((f) =>
              f.type.toLowerCase().startsWith("image/")
          )
        : [];

/** What a failed upload says: the API's reason, or a retry hint when there was no answer. */
export function uploadErrorText(
    status: number | undefined,
    serverMessage: string | undefined
): string {
    if (status === 413) return IMAGE_MESSAGES.tooLarge;
    if (status === 415) return serverMessage ?? IMAGE_MESSAGES.unsupported;
    if (status === undefined) return IMAGE_MESSAGES.uploadFailed;
    return serverMessage ?? IMAGE_MESSAGES.uploadFailed;
}

/**
 * 16b's `errors.imageIds` on a note's POST or PUT (the 10-image limit, a duplicate, an
 * upload that could not be attached, or the 409 race), or undefined for any other error.
 */
export function imageIdsErrorFrom(error: unknown): string | undefined {
    const data = (
        error as
            | { response?: { data?: { errors?: Record<string, unknown> } } }
            | undefined
    )?.response?.data;
    const errors = data?.errors;
    if (!errors || typeof errors !== "object") return undefined;
    const key = Object.keys(errors).find((k) => k.toLowerCase() === "imageids");
    const messages = key ? errors[key] : undefined;
    return Array.isArray(messages) && typeof messages[0] === "string"
        ? messages[0]
        : undefined;
}

// ── Attachments ──────────────────────────────────────────────────────────────

export type AttachmentStatus = "preparing" | "uploading" | "ready" | "failed";

/** One image in the composer or the note editor. */
export type Attachment = {
    /** Local. */
    key: string;
    status: AttachmentStatus;
    /** An object URL (revoked on remove and after a post), or "" to draw the `thumb`. */
    previewUrl: string;
    /** 0–1, from axios's `onUploadProgress`. */
    progress: number;
    image?: NoteImage;
    error?: string;
    /** Already on the note being edited: removing it deletes it on save, not now. */
    onNote?: boolean;
};

export const newAttachment = (key: string, previewUrl: string): Attachment => ({
    key,
    status: "preparing",
    previewUrl,
    progress: 0,
});

/** Images that are already uploaded (a draft's, or a note's in the editor) as ready attachments. */
export const attachmentsFromImages = (
    images: readonly NoteImage[],
    options: { onNote?: boolean } = {}
): Attachment[] =>
    images.map((image) => ({
        key: `image-${image.id}`,
        status: "ready",
        previewUrl: "",
        progress: 1,
        image: { id: image.id, width: image.width, height: image.height },
        ...(options.onNote ? { onNote: true } : {}),
    }));

/** How many of `count` new files fit, and whether some were refused (the 11th). */
export function attachmentSlots(
    list: readonly Attachment[],
    count: number
): { take: number; refused: boolean } {
    const take = Math.max(0, Math.min(count, NOTE_IMAGES_MAX - list.length));
    return { take, refused: take < count };
}

/** The list with one attachment changed; the same list when it is gone (removed meanwhile). */
export function updateAttachment(
    list: Attachment[],
    key: string,
    patch: Partial<Attachment>
): Attachment[] {
    const index = list.findIndex((a) => a.key === key);
    if (index === -1) return list;
    const next = { ...list[index], ...patch };
    if (patch.error === undefined && "error" in patch) delete next.error;
    return list.map((a, i) => (i === index ? next : a));
}

export const hasAttachment = (list: readonly Attachment[], key: string) =>
    list.some((a) => a.key === key);

export const markUploading = (list: Attachment[], key: string) =>
    updateAttachment(list, key, {
        status: "uploading",
        progress: 0,
        error: undefined,
    });

export const markProgress = (
    list: Attachment[],
    key: string,
    progress: number
) =>
    updateAttachment(list, key, {
        progress: Math.min(1, Math.max(0, progress)),
    });

export const markReady = (list: Attachment[], key: string, image: NoteImage) =>
    updateAttachment(list, key, {
        status: "ready",
        progress: 1,
        image: { id: image.id, width: image.width, height: image.height },
        error: undefined,
    });

export const markFailed = (list: Attachment[], key: string, error: string) =>
    updateAttachment(list, key, { status: "failed", error });

/**
 * ✕ on an attachment. `deleteImageId` is the upload to `DELETE` now: a ready image
 * that is on no note. One already on the note is deleted by the note's save.
 */
export function removeAttachment(
    list: Attachment[],
    key: string
): { list: Attachment[]; removed?: Attachment; deleteImageId?: string } {
    const removed = list.find((a) => a.key === key);
    if (!removed) return { list };
    return {
        list: list.filter((a) => a.key !== key),
        removed,
        ...(removed.status === "ready" && removed.image && !removed.onNote
            ? { deleteImageId: removed.image.id }
            : {}),
    };
}

/** Moves an attachment one place left (-1) or right (+1); unchanged at either end. */
export function moveAttachment(
    list: Attachment[],
    key: string,
    delta: -1 | 1
): Attachment[] {
    const index = list.findIndex((a) => a.key === key);
    const target = index + delta;
    if (index === -1 || target < 0 || target >= list.length) return list;
    const next = [...list];
    [next[index], next[target]] = [next[target], next[index]];
    return next;
}

/** ➤ waits while any attachment is still being prepared or uploaded. */
export const attachmentsBusy = (list: readonly Attachment[]) =>
    list.some((a) => a.status === "preparing" || a.status === "uploading");

/** ➤ is off while any attachment failed (retry it, or ✕). */
export const attachmentsFailed = (list: readonly Attachment[]) =>
    list.some((a) => a.status === "failed");

/** The uploaded images, in order: what `imageIds` sends and the optimistic note draws. */
export const readyImages = (list: readonly Attachment[]): NoteImage[] =>
    list.flatMap((a) => (a.status === "ready" && a.image ? [a.image] : []));

/** Whether the editor's list differs from the note's images (ids and order). */
export function imagesChanged(
    before: readonly NoteImage[],
    list: readonly Attachment[]
): boolean {
    const after = list.map((a) => a.image?.id ?? a.key);
    return (
        after.length !== before.length ||
        after.some((id, i) => id !== before[i].id)
    );
}

/** How many of the note's own images the editor's list no longer has. */
export const removedNoteImages = (
    before: readonly NoteImage[],
    list: readonly Attachment[]
) =>
    before.filter((image) => !list.some((a) => a.image?.id === image.id))
        .length;

/**
 * After the API refused the ids (16b's `errors.imageIds`: swept, or attached elsewhere
 * at the same time): every uploaded attachment that is on no note must go up again.
 */
export const failUploads = (list: Attachment[], error: string): Attachment[] =>
    list.map((a) =>
        a.status === "ready" && !a.onNote
            ? { ...a, status: "failed" as const, error }
            : a
    );

// ── Drafts ───────────────────────────────────────────────────────────────────

/**
 * The images of a saved draft (15d's JSON gains `images`). Each carries its size, so
 * the optimistic note lays out before any byte arrives. Anything malformed is dropped.
 */
export function draftImages(value: unknown): NoteImage[] {
    if (!Array.isArray(value)) return [];
    return value
        .filter(
            (i): i is NoteImage =>
                !!i &&
                typeof i === "object" &&
                typeof (i as NoteImage).id === "string" &&
                Number.isFinite((i as NoteImage).width) &&
                Number.isFinite((i as NoteImage).height)
        )
        .slice(0, NOTE_IMAGES_MAX)
        .map((i) => ({ id: i.id, width: i.width, height: i.height }));
}

// ── The caption nudge (§3, §5) ───────────────────────────────────────────────

/**
 * ➤ with images and no caption asks first ("confirm"); Post anyway sets `confirmed`.
 * A caption, or no images, posts at once.
 */
export function captionNudge(args: {
    text: string;
    imageCount: number;
    confirmed: boolean;
}): "post" | "confirm" {
    return args.imageCount > 0 && args.text.trim() === "" && !args.confirmed
        ? "confirm"
        : "post";
}

/**
 * "⚠ Tag what's in this?" under an image note with no mention, for its author only
 * (§5: images are resolved by their author). The note-level half of step 19's loose end.
 */
export function showTagHint(
    note: Pick<SessionNote, "images" | "text" | "authorMemberId">,
    viewerMemberId: string | undefined
): boolean {
    return (
        !!viewerMemberId &&
        note.authorMemberId === viewerMemberId &&
        (note.images?.length ?? 0) > 0 &&
        mentionedEntryIds(note.text).length === 0
    );
}

// ── Drawing ──────────────────────────────────────────────────────────────────

export type ImageLayout = {
    /** single: full width; pair: side by side; trio: one large and two small; grid: 2 × 2. */
    kind: "none" | "single" | "pair" | "trio" | "grid";
    /** How many tiles are drawn. */
    shown: number;
    /** "+n" on the last tile: the images not drawn. */
    more: number;
};

export function imageLayout(count: number): ImageLayout {
    if (count <= 0) return { kind: "none", shown: 0, more: 0 };
    if (count === 1) return { kind: "single", shown: 1, more: 0 };
    if (count === 2) return { kind: "pair", shown: 2, more: 0 };
    if (count === 3) return { kind: "trio", shown: 3, more: 0 };
    return { kind: "grid", shown: 4, more: count - 4 };
}

/** An image's `alt`: the caption as plain text, or "Image from Sam". */
export function imageAlt(text: string, authorName: string): string {
    const plain = notePlainText(text);
    if (!plain) return `Image from ${authorName}`;
    return plain.length > 300 ? `${plain.slice(0, 299)}…` : plain;
}

// ── The viewer ───────────────────────────────────────────────────────────────

/** `?image={imageId}`: the open viewer, so the phone's back gesture closes it. */
export const IMAGE_PARAM = "image";

/** The next index in the viewer, stopping at either end. */
export const stepImage = (index: number, count: number, delta: number) =>
    Math.min(Math.max(0, index + delta), Math.max(0, count - 1));

/**
 * A horizontal swipe: -1 (to the previous image, finger moved right), 1 (to the next),
 * or 0 when it was short or mostly vertical.
 */
export function swipeStep(dx: number, dy: number, threshold = 50): -1 | 0 | 1 {
    if (Math.abs(dx) < threshold || Math.abs(dx) < Math.abs(dy) * 1.5) return 0;
    return dx < 0 ? 1 : -1;
}
