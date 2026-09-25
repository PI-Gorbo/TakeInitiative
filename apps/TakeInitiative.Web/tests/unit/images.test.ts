import { describe, expect, it, vi } from "vitest";
import {
    IMAGE_MESSAGES,
    IMAGE_UPLOAD_MAX_BYTES,
    NOTE_IMAGES_MAX,
    attachmentSlots,
    attachmentsBusy,
    attachmentsFailed,
    attachmentsFromImages,
    captionNudge,
    draftImages,
    failUploads,
    imageAlt,
    imageFiles,
    imageIdsErrorFrom,
    imageLayout,
    imageUrl,
    imagesChanged,
    markFailed,
    markProgress,
    markReady,
    markUploading,
    moveAttachment,
    newAttachment,
    prepareDecision,
    prepareImage,
    readyImages,
    redrawSize,
    removeAttachment,
    removedNoteImages,
    showTagHint,
    stepImage,
    swipeStep,
    uploadErrorText,
    type Attachment,
    type PrepareDeps,
} from "~/utils/images";

const MB = 1024 * 1024;
const image = (id: string) => ({ id, width: 640, height: 480 });
const ENTRY = "0b7c5e1a-8f3d-4c2b-9a61-2d4e8f00a0ff";

describe("imageUrl", () => {
    it("builds the API's variant URL on the API's base", () => {
        expect(imageUrl("http://localhost:5010", "c1", "i1", "thumb")).toBe(
            "http://localhost:5010/api/campaigns/c1/images/i1/thumb"
        );
        expect(
            imageUrl("https://api.example.com/", "c 1", "i1", "display")
        ).toBe("https://api.example.com/api/campaigns/c%201/images/i1/display");
        expect(imageUrl(undefined, "c1", "i1", "thumb")).toBe(
            "/api/campaigns/c1/images/i1/thumb"
        );
    });
});

describe("prepareDecision", () => {
    const decoded = { width: 4000, height: 3000 };

    it("uploads JPEG, PNG, WebP and GIF unchanged within 20 MB and 8000 px", () => {
        for (const type of [
            "image/jpeg",
            "image/png",
            "image/webp",
            "image/gif",
        ]) {
            expect(prepareDecision({ type, size: 5 * MB }, decoded)).toEqual({
                action: "upload",
            });
        }
        expect(
            prepareDecision(
                { type: "image/jpeg", size: IMAGE_UPLOAD_MAX_BYTES },
                { width: 8000, height: 10 }
            )
        ).toEqual({
            action: "upload",
        });
    });

    it("redraws an accepted type that is too large or too wide, at most 4096 px", () => {
        expect(
            prepareDecision(
                { type: "image/jpeg", size: IMAGE_UPLOAD_MAX_BYTES + 1 },
                decoded
            )
        ).toEqual({
            action: "redraw",
            width: 4000,
            height: 3000,
        });
        expect(
            prepareDecision(
                { type: "image/jpeg", size: IMAGE_UPLOAD_MAX_BYTES + 1 },
                { width: 6000, height: 4000 }
            )
        ).toEqual({
            action: "redraw",
            width: 4096,
            height: 2731,
        });
        expect(
            prepareDecision(
                { type: "image/png", size: MB },
                { width: 1000, height: 8001 }
            )
        ).toEqual({
            action: "redraw",
            width: 512,
            height: 4096,
        });
    });

    it("redraws another type the browser can decode (HEIC on Safari), not upscaled", () => {
        expect(
            prepareDecision(
                { type: "image/heic", size: 3 * MB },
                { width: 3024, height: 4032 }
            )
        ).toEqual({
            action: "redraw",
            width: 3024,
            height: 4032,
        });
        expect(
            prepareDecision(
                { type: "image/heic", size: 3 * MB },
                { width: 6048, height: 8064 }
            )
        ).toEqual({
            action: "redraw",
            width: 3072,
            height: 4096,
        });
        expect(
            prepareDecision(
                { type: "image/avif", size: MB },
                { width: 800, height: 600 }
            )
        ).toEqual({
            action: "redraw",
            width: 800,
            height: 600,
        });
    });

    it("leaves an accepted type the browser cannot decode to the API, and fails anything else", () => {
        expect(prepareDecision({ type: "image/gif", size: MB }, null)).toEqual({
            action: "upload",
        });
        expect(prepareDecision({ type: "image/heic", size: MB }, null)).toEqual(
            {
                action: "fail",
                error: IMAGE_MESSAGES.unsupported,
            }
        );
        expect(prepareDecision({ type: "", size: MB }, null)).toEqual({
            action: "fail",
            error: IMAGE_MESSAGES.unsupported,
        });
        expect(
            prepareDecision({ type: "image/jpeg", size: 25 * MB }, null)
        ).toEqual({
            action: "fail",
            error: IMAGE_MESSAGES.tooLarge,
        });
    });

    it("scales to the long edge and never up", () => {
        expect(redrawSize(6000, 1000, 4096)).toEqual({
            width: 4096,
            height: 683,
        });
        expect(redrawSize(100, 50, 4096)).toEqual({ width: 100, height: 50 });
        expect(redrawSize(10000, 1, 4096)).toEqual({ width: 4096, height: 1 });
    });
});

describe("prepareImage", () => {
    const file = (type: string, size: number, name = "photo.heic") =>
        ({ type, size, name }) as unknown as File;
    const deps = (
        decoded: { width: number; height: number } | null,
        encoded: Blob | null = new Blob(["x"])
    ) => {
        const close = vi.fn();
        const encode = vi.fn(async () => encoded);
        const value: PrepareDeps = {
            decode: async () => (decoded ? { ...decoded, close } : null),
            encode,
        };
        return { value, close, encode };
    };

    it("sends an ordinary photo as it is, and closes the decoded bitmap", async () => {
        const photo = file("image/jpeg", 2 * MB, "cave.jpg");
        const { value, close, encode } = deps({ width: 4000, height: 3000 });
        expect(await prepareImage(photo, value)).toEqual({
            ok: true,
            file: photo,
            fileName: "cave.jpg",
        });
        expect(encode).not.toHaveBeenCalled();
        expect(close).toHaveBeenCalledOnce();
    });

    it("redraws HEIC as a JPEG at 4096 px", async () => {
        const { value, encode } = deps({ width: 6048, height: 8064 });
        const result = await prepareImage(file("image/heic", 3 * MB), value);
        expect(result).toMatchObject({ ok: true, fileName: "photo.jpg" });
        expect(encode).toHaveBeenCalledWith(expect.anything(), {
            action: "redraw",
            width: 3072,
            height: 4096,
        });
    });

    it("fails without an upload when nothing can read it, or the redraw is still over 20 MB", async () => {
        expect(
            await prepareImage(file("application/pdf", MB), deps(null).value)
        ).toEqual({
            ok: false,
            error: IMAGE_MESSAGES.unsupported,
        });
        expect(
            await prepareImage(
                file("image/heic", MB),
                deps({ width: 10, height: 10 }, null).value
            )
        ).toEqual({
            ok: false,
            error: IMAGE_MESSAGES.unsupported,
        });
        const huge = { size: IMAGE_UPLOAD_MAX_BYTES + 1 } as Blob;
        expect(
            await prepareImage(
                file("image/heic", MB),
                deps({ width: 10, height: 10 }, huge).value
            )
        ).toEqual({
            ok: false,
            error: IMAGE_MESSAGES.tooLarge,
        });
    });

    it("treats a decoder that throws as one that cannot decode", async () => {
        const gif = file("image/gif", MB, "a.gif");
        const value: PrepareDeps = {
            decode: () => Promise.reject(new Error("no")),
            encode: async () => null,
        };
        expect(await prepareImage(gif, value)).toEqual({
            ok: true,
            file: gif,
            fileName: "a.gif",
        });
    });
});

describe("attachments", () => {
    const list = (): Attachment[] => [
        { ...newAttachment("a", "blob:a") },
        { key: "b", status: "uploading", previewUrl: "blob:b", progress: 0.5 },
        {
            key: "c",
            status: "ready",
            previewUrl: "blob:c",
            progress: 1,
            image: image("ic"),
        },
        {
            key: "d",
            status: "failed",
            previewUrl: "blob:d",
            progress: 0,
            error: "no",
        },
    ];

    it("goes preparing → uploading → ready, with progress clamped", () => {
        let items = [newAttachment("x", "blob:x")];
        expect(items[0]).toEqual({
            key: "x",
            status: "preparing",
            previewUrl: "blob:x",
            progress: 0,
        });
        items = markUploading(items, "x");
        expect(items[0].status).toBe("uploading");
        items = markProgress(items, "x", 1.4);
        expect(items[0].progress).toBe(1);
        items = markProgress(items, "x", 0.3);
        expect(items[0].progress).toBe(0.3);
        items = markReady(items, "x", {
            ...image("i1"),
            uploadedAt: "2026-09-25T00:00:00Z",
        } as never);
        expect(items[0]).toEqual({
            key: "x",
            status: "ready",
            previewUrl: "blob:x",
            progress: 1,
            image: image("i1"),
        });
    });

    it("fails, and clears the error when uploading again", () => {
        let items = markFailed([newAttachment("x", "")], "x", "boom");
        expect(items[0]).toMatchObject({ status: "failed", error: "boom" });
        items = markUploading(items, "x");
        expect(items[0]).not.toHaveProperty("error");
    });

    it("ignores an update for an attachment that was removed meanwhile", () => {
        const items = list();
        expect(markReady(items, "gone", image("i"))).toBe(items);
        expect(markProgress(items, "gone", 0.5)).toBe(items);
    });

    it("✕ on each status: only a ready upload on no note is deleted", () => {
        for (const key of ["a", "b", "d"]) {
            const result = removeAttachment(list(), key);
            expect(result.list.map((a) => a.key)).not.toContain(key);
            expect(result.removed?.key).toBe(key);
            expect(result.deleteImageId).toBeUndefined();
        }
        expect(removeAttachment(list(), "c").deleteImageId).toBe("ic");
        const onNote = attachmentsFromImages([image("n1")], { onNote: true });
        expect(removeAttachment(onNote, "image-n1")).toEqual({
            list: [],
            removed: onNote[0],
        });
        const none = list();
        expect(removeAttachment(none, "zz")).toEqual({ list: none });
    });

    it("allows at most 10, refusing the 11th", () => {
        const nine = Array.from({ length: 9 }, (_, i) =>
            newAttachment(`k${i}`, "")
        );
        expect(attachmentSlots(nine, 1)).toEqual({ take: 1, refused: false });
        expect(attachmentSlots(nine, 3)).toEqual({ take: 1, refused: true });
        expect(attachmentSlots([...nine, newAttachment("k9", "")], 1)).toEqual({
            take: 0,
            refused: true,
        });
        expect(attachmentSlots([], 12)).toEqual({
            take: NOTE_IMAGES_MAX,
            refused: true,
        });
        expect(IMAGE_MESSAGES.tooMany).toBe(
            "A note can have at most 10 images."
        );
    });

    it("knows when ➤ must wait and when it is off", () => {
        expect(attachmentsBusy(list())).toBe(true);
        expect(attachmentsBusy(list().slice(2))).toBe(false);
        expect(attachmentsFailed(list())).toBe(true);
        expect(attachmentsFailed(list().slice(0, 3))).toBe(false);
        expect(readyImages(list())).toEqual([image("ic")]);
    });

    it("moves left and right, not past either end", () => {
        const items = list();
        expect(moveAttachment(items, "a", 1).map((a) => a.key)).toEqual([
            "b",
            "a",
            "c",
            "d",
        ]);
        expect(moveAttachment(items, "c", -1).map((a) => a.key)).toEqual([
            "a",
            "c",
            "b",
            "d",
        ]);
        expect(moveAttachment(items, "a", -1)).toBe(items);
        expect(moveAttachment(items, "d", 1)).toBe(items);
    });

    it("tells the editor whether the images changed, and how many will be deleted", () => {
        const before = [image("1"), image("2"), image("3")];
        const same = attachmentsFromImages(before, { onNote: true });
        expect(imagesChanged(before, same)).toBe(false);
        expect(imagesChanged(before, moveAttachment(same, "image-2", 1))).toBe(
            true
        );
        expect(imagesChanged(before, same.slice(1))).toBe(true);
        expect(imagesChanged(before, [...same, newAttachment("x", "")])).toBe(
            true
        );
        expect(removedNoteImages(before, same.slice(1))).toBe(1);
        expect(removedNoteImages(before, same)).toBe(0);
    });

    it("sends new uploads back to failed when the API refuses the ids", () => {
        const items = [
            ...attachmentsFromImages([image("n")], { onNote: true }),
            ...list(),
        ];
        const next = failUploads(items, "Upload it again.");
        expect(next[0].status).toBe("ready");
        expect(next.find((a) => a.key === "c")).toMatchObject({
            status: "failed",
            error: "Upload it again.",
        });
        expect(next.find((a) => a.key === "b")?.status).toBe("uploading");
    });

    it("picks the images out of a paste or a drop", () => {
        const files = [
            { type: "image/png" },
            { type: "application/pdf" },
            { type: "IMAGE/HEIC" },
        ] as File[];
        expect(imageFiles(files)).toEqual([files[0], files[2]]);
        expect(imageFiles(null)).toEqual([]);
    });
});

describe("errors", () => {
    it("words a failed upload", () => {
        expect(uploadErrorText(413, undefined)).toBe(IMAGE_MESSAGES.tooLarge);
        expect(
            uploadErrorText(
                415,
                "This image type is not supported. Try JPEG or PNG."
            )
        ).toBe(IMAGE_MESSAGES.unsupported);
        expect(uploadErrorText(409, "Post or remove some images first.")).toBe(
            "Post or remove some images first."
        );
        expect(uploadErrorText(undefined, undefined)).toBe(
            IMAGE_MESSAGES.uploadFailed
        );
    });

    it("reads 16b's errors.imageIds and nothing else", () => {
        const error = (errors: Record<string, string[]>) => ({
            response: { data: { statusCode: 400, message: "", errors } },
        });
        expect(
            imageIdsErrorFrom(
                error({
                    imageIds: [
                        "An image could not be attached. Upload it again.",
                    ],
                })
            )
        ).toBe("An image could not be attached. Upload it again.");
        expect(
            imageIdsErrorFrom(
                error({ text: ["A session note needs some text or an image."] })
            )
        ).toBeUndefined();
        expect(imageIdsErrorFrom(new Error("network"))).toBeUndefined();
    });
});

describe("drafts", () => {
    it("keeps well-formed images, at most 10", () => {
        expect(
            draftImages([
                image("a"),
                { id: "b" },
                null,
                "c",
                { ...image("d"), extra: 1 },
            ])
        ).toEqual([image("a"), image("d")]);
        expect(
            draftImages(Array.from({ length: 12 }, (_, i) => image(`${i}`)))
        ).toHaveLength(10);
        expect(draftImages(undefined)).toEqual([]);
    });
});

describe("captionNudge", () => {
    it("posts at once with no images", () => {
        expect(
            captionNudge({ text: "", imageCount: 0, confirmed: false })
        ).toBe("post");
    });

    it("asks first with images and no caption, and posts after Post anyway", () => {
        expect(
            captionNudge({ text: "  ", imageCount: 2, confirmed: false })
        ).toBe("confirm");
        expect(
            captionNudge({ text: "  ", imageCount: 2, confirmed: true })
        ).toBe("post");
    });

    it("skips the confirm when there is a caption, even with no mention", () => {
        expect(
            captionNudge({ text: "the map", imageCount: 1, confirmed: false })
        ).toBe("post");
    });
});

describe("showTagHint", () => {
    const note = (
        text: string,
        images = [image("i")],
        authorMemberId = "me"
    ) => ({ text, images, authorMemberId });

    it("shows the author an image note with no mention", () => {
        expect(showTagHint(note(""), "me")).toBe(true);
        expect(showTagHint(note("a map"), "me")).toBe(true);
    });

    it("hides it from everyone else, on a mention, and on a text note", () => {
        expect(showTagHint(note(""), "other")).toBe(false);
        expect(
            showTagHint(note(`Map of @[Cragmaw](entry:${ENTRY})`), "me")
        ).toBe(false);
        expect(showTagHint(note("text", []), "me")).toBe(false);
        expect(showTagHint(note(""), undefined)).toBe(false);
    });
});

describe("imageLayout", () => {
    it("lays out 1 to 6 images", () => {
        expect(imageLayout(0)).toEqual({ kind: "none", shown: 0, more: 0 });
        expect(imageLayout(1)).toEqual({ kind: "single", shown: 1, more: 0 });
        expect(imageLayout(2)).toEqual({ kind: "pair", shown: 2, more: 0 });
        expect(imageLayout(3)).toEqual({ kind: "trio", shown: 3, more: 0 });
        expect(imageLayout(4)).toEqual({ kind: "grid", shown: 4, more: 0 });
        expect(imageLayout(5)).toEqual({ kind: "grid", shown: 4, more: 1 });
        expect(imageLayout(6)).toEqual({ kind: "grid", shown: 4, more: 2 });
    });
});

describe("imageAlt", () => {
    it("is the caption as plain text, or who posted it", () => {
        expect(
            imageAlt(
                `Map of @[Cragmaw Hideout](entry:${ENTRY}), **found** on Klarg`,
                "Sam"
            )
        ).toBe("Map of Cragmaw Hideout, found on Klarg");
        expect(imageAlt("", "Sam")).toBe("Image from Sam");
        expect(imageAlt("x".repeat(400), "Sam")).toHaveLength(300);
    });
});

describe("the viewer", () => {
    it("steps through a note's images, stopping at either end", () => {
        expect(stepImage(0, 3, 1)).toBe(1);
        expect(stepImage(2, 3, 1)).toBe(2);
        expect(stepImage(0, 3, -1)).toBe(0);
        expect(stepImage(0, 1, 1)).toBe(0);
    });

    it("reads a swipe: left is the next image, right the previous, short or vertical is none", () => {
        expect(swipeStep(-120, 10)).toBe(1);
        expect(swipeStep(120, 10)).toBe(-1);
        expect(swipeStep(-30, 0)).toBe(0);
        expect(swipeStep(-80, 100)).toBe(0);
    });
});
