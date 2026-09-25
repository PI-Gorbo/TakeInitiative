import axios from "axios";
import type { MaybeRefOrGetter, Ref } from "vue";
import { toast } from "vue-sonner";
import { apiErrorMessage, apiErrorStatus } from "~/utils/apiErrorParser";
import {
    IMAGE_MESSAGES,
    IMAGE_REDRAW_QUALITY,
    attachmentSlots,
    attachmentsBusy,
    attachmentsFailed,
    hasAttachment,
    imageUrl,
    markFailed,
    markProgress,
    markReady,
    markUploading,
    moveAttachment,
    newAttachment,
    prepareImage,
    readyImages,
    removeAttachment,
    updateAttachment,
    uploadErrorText,
    type Attachment,
    type ImageVariant,
    type PrepareDeps,
} from "~/utils/images";

/** `imageUrl` with the API's base URL: `src` for a plain `<img>` (16c). */
export function useImageUrl() {
    const base = useRuntimeConfig().public.axios?.baseURL as string | undefined;
    return (campaignId: string, imageId: string, variant: ImageVariant) =>
        imageUrl(base, campaignId, imageId, variant);
}

/** The browser's half of `prepareImage`: `createImageBitmap` and a canvas. */
const browserPrepare: PrepareDeps = {
    decode: async (file) =>
        typeof createImageBitmap === "function"
            ? await createImageBitmap(file)
            : null,
    encode: (image, size) =>
        new Promise((resolve) => {
            const canvas = document.createElement("canvas");
            canvas.width = size.width;
            canvas.height = size.height;
            const context = canvas.getContext("2d");
            if (!context) return resolve(null);
            // JPEG has no alpha: a transparent PNG gets a white ground, not black.
            context.fillStyle = "#fff";
            context.fillRect(0, 0, size.width, size.height);
            context.drawImage(
                image as unknown as CanvasImageSource,
                0,
                0,
                size.width,
                size.height
            );
            canvas.toBlob(resolve, "image/jpeg", IMAGE_REDRAW_QUALITY);
        }),
};

let keySeed = 0;
const newKey = () =>
    `att-${Date.now().toString(36)}-${(keySeed++).toString(36)}`;
const isObjectUrl = (url: string) => url.startsWith("blob:");

/**
 * The upload queue behind the composer's and the note editor's `AttachmentStrip`
 * (16c). `attachments` is the caller's list (the composer's state, or the editor's);
 * every change replaces it through the pure functions in `utils/images.ts`.
 *
 * - `add(files)` attaches up to 10 in all. Each is prepared (`prepareImage`) and
 *   uploaded at once, so ➤ is quick later.
 * - `remove(key)` is ✕: it aborts an upload, revokes the preview and `DELETE`s a
 *   ready upload that is on no note (a failure there is left to the sweeper).
 * - `retry(key)` uploads a failed one again from its file; one with no file (a
 *   draft's) is removed instead.
 * - `release(list)` forgets attachments that were posted: revokes their previews.
 * - `discard()` removes every attachment that is not on the note (the editor's Cancel).
 */
export function useImageAttachments(options: {
    attachments: Ref<Attachment[]>;
    campaignId: MaybeRefOrGetter<string>;
}) {
    const api = useApi();
    const list = options.attachments;
    const sources = new Map<string, File>();
    const controllers = new Map<string, AbortController>();

    const busy = computed(() => attachmentsBusy(list.value));
    const failed = computed(() => attachmentsFailed(list.value));
    const images = computed(() => readyImages(list.value));

    function add(files: readonly File[]) {
        if (files.length === 0) return;
        const { take, refused } = attachmentSlots(list.value, files.length);
        if (refused) toast.error(IMAGE_MESSAGES.tooMany);
        for (const file of files.slice(0, take)) {
            const key = newKey();
            sources.set(key, file);
            list.value = [
                ...list.value,
                newAttachment(key, URL.createObjectURL(file)),
            ];
            void upload(key);
        }
    }

    async function upload(key: string) {
        const file = sources.get(key);
        if (!file) return;
        list.value = updateAttachment(list.value, key, {
            status: "preparing",
            progress: 0,
            error: undefined,
        });
        const prepared = await prepareImage(file, browserPrepare);
        if (!hasAttachment(list.value, key)) return;
        if (!prepared.ok) {
            list.value = markFailed(list.value, key, prepared.error);
            return;
        }

        list.value = markUploading(list.value, key);
        const controller = new AbortController();
        controllers.set(key, controller);
        try {
            const image = await api.image.post({
                campaignId: toValue(options.campaignId),
                file: prepared.file,
                fileName: prepared.fileName,
                signal: controller.signal,
                onProgress: (progress) =>
                    (list.value = markProgress(list.value, key, progress)),
            });
            // ✕ while it went up: nobody wants it any more.
            if (!hasAttachment(list.value, key)) deleteQuietly(image.id);
            else list.value = markReady(list.value, key, image);
        } catch (error) {
            if (axios.isCancel(error) || !hasAttachment(list.value, key))
                return;
            const message = apiErrorMessage(error, "") || undefined;
            list.value = markFailed(
                list.value,
                key,
                uploadErrorText(apiErrorStatus(error), message)
            );
        } finally {
            controllers.delete(key);
        }
    }

    function deleteQuietly(imageId: string) {
        api.image
            .delete({ campaignId: toValue(options.campaignId), imageId })
            .catch(() => {
                // The sweeper deletes it after 24 hours.
            });
    }

    function forget(attachment: Attachment) {
        controllers.get(attachment.key)?.abort();
        controllers.delete(attachment.key);
        sources.delete(attachment.key);
        if (isObjectUrl(attachment.previewUrl))
            URL.revokeObjectURL(attachment.previewUrl);
    }

    function remove(key: string) {
        const result = removeAttachment(list.value, key);
        if (!result.removed) return;
        list.value = result.list;
        forget(result.removed);
        if (result.deleteImageId) deleteQuietly(result.deleteImageId);
    }

    function retry(key: string) {
        if (sources.has(key)) void upload(key);
        else remove(key);
    }

    const move = (key: string, delta: -1 | 1) =>
        (list.value = moveAttachment(list.value, key, delta));

    function release(attachments: readonly Attachment[]) {
        for (const attachment of attachments) forget(attachment);
    }

    function discard() {
        for (const attachment of list.value)
            if (!attachment.onNote) remove(attachment.key);
    }

    onBeforeUnmount(() => {
        for (const controller of controllers.values()) controller.abort();
        for (const attachment of list.value)
            if (isObjectUrl(attachment.previewUrl))
                URL.revokeObjectURL(attachment.previewUrl);
    });

    return {
        attachments: list,
        add,
        remove,
        retry,
        move,
        release,
        discard,
        busy,
        failed,
        images,
    };
}

export type ImageAttachments = ReturnType<typeof useImageAttachments>;
