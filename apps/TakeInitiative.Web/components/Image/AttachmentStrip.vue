<template>
    <!-- The images being attached (16c): 64px thumbnails with progress, ✕ and retry,
         scrolling sideways. In the composer it sits in the strip area above the
         toolbar, so on a phone it stays above the keyboard. The note editor also moves
         them left and right. -->
    <div
        v-if="attachments.length > 0"
        class="flex flex-col gap-1">
        <ul
            class="-mx-1 flex gap-2 overflow-x-auto px-1 py-1"
            aria-label="Attached images">
            <li
                v-for="(attachment, index) in attachments"
                :key="attachment.key"
                class="flex shrink-0 flex-col items-center gap-0.5">
                <div
                    :class="[
                        'relative size-16 overflow-hidden rounded-md border bg-muted',
                        attachment.status === 'failed' && 'border-destructive',
                    ]">
                    <img
                        :src="
                            attachment.previewUrl ||
                            (attachment.image
                                ? src(campaignId, attachment.image.id, 'thumb')
                                : '')
                        "
                        :alt="`Image ${index + 1}`"
                        width="64"
                        height="64"
                        decoding="async"
                        class="size-full object-cover"
                        @error="onError(attachment)" />
                    <!-- Preparing and uploading: a bar along the bottom. -->
                    <div
                        v-if="
                            attachment.status === 'preparing' ||
                            attachment.status === 'uploading'
                        "
                        class="absolute inset-0 flex items-center justify-center bg-black/40"
                        role="progressbar"
                        :aria-label="`Uploading image ${index + 1}`"
                        :aria-valuenow="Math.round(attachment.progress * 100)"
                        aria-valuemin="0"
                        aria-valuemax="100">
                        <LoaderCircle
                            class="size-5 animate-spin text-white"
                            aria-hidden="true" />
                        <div
                            class="absolute inset-x-0 bottom-0 h-1 bg-white/30">
                            <div
                                class="h-full bg-gold transition-[width]"
                                :style="{
                                    width: `${Math.round(attachment.progress * 100)}%`,
                                }" />
                        </div>
                    </div>
                    <!-- Failed: tap to retry. -->
                    <button
                        v-if="attachment.status === 'failed'"
                        type="button"
                        class="absolute inset-0 flex items-center justify-center bg-black/60 text-white"
                        :title="attachment.error"
                        :aria-label="`Retry image ${index + 1}: ${attachment.error ?? 'the upload failed'}`"
                        @mousedown.prevent
                        @click="emit('retry', attachment.key)">
                        <RotateCw
                            class="size-5"
                            aria-hidden="true" />
                    </button>
                    <button
                        type="button"
                        class="absolute right-0 top-0 flex size-8 items-center justify-center rounded-bl-md bg-black/60 text-white hover:bg-black/80"
                        :aria-label="`Remove image ${index + 1}`"
                        @mousedown.prevent
                        @click="emit('remove', attachment.key)">
                        <X
                            class="size-4"
                            aria-hidden="true" />
                    </button>
                </div>
                <div
                    v-if="movable && attachments.length > 1"
                    class="flex">
                    <button
                        type="button"
                        class="flex h-11 w-8 items-center justify-center rounded text-muted-foreground hover:bg-accent disabled:opacity-30 md:h-7"
                        :aria-label="`Move image ${index + 1} left`"
                        :disabled="index === 0"
                        @click="emit('move', attachment.key, -1)">
                        <ChevronLeft
                            class="size-4"
                            aria-hidden="true" />
                    </button>
                    <button
                        type="button"
                        class="flex h-11 w-8 items-center justify-center rounded text-muted-foreground hover:bg-accent disabled:opacity-30 md:h-7"
                        :aria-label="`Move image ${index + 1} right`"
                        :disabled="index === attachments.length - 1"
                        @click="emit('move', attachment.key, 1)">
                        <ChevronRight
                            class="size-4"
                            aria-hidden="true" />
                    </button>
                </div>
            </li>
        </ul>
        <p
            v-if="failure"
            class="text-xs text-destructive-tint"
            role="alert">
            {{ failure }}
        </p>
        <slot />
    </div>
</template>

<script setup lang="ts">
    import {
        ChevronLeft,
        ChevronRight,
        LoaderCircle,
        RotateCw,
        X,
    } from "lucide-vue-next";
    import type { Attachment } from "~/utils/images";

    const props = defineProps<{
        campaignId: string;
        attachments: Attachment[];
        /** The note editor: move left and right buttons (16c). */
        movable?: boolean;
    }>();
    const emit = defineEmits<{
        remove: [key: string];
        retry: [key: string];
        move: [key: string, delta: -1 | 1];
        /** A draft's upload whose `thumb` did not load (swept): dropped quietly. */
        missing: [key: string];
    }>();

    const src = useImageUrl();
    const failure = computed(
        () => props.attachments.find((a) => a.status === "failed")?.error
    );

    function onError(attachment: Attachment) {
        if (
            !attachment.previewUrl &&
            attachment.status === "ready" &&
            !attachment.onNote
        )
            emit("missing", attachment.key);
    }
</script>
