<template>
    <!-- The full-screen image viewer (16c). It follows `?image={imageId}`, so the
         phone's back gesture closes it. The `display` variant, with swipe and the
         arrow keys through the note's images, pinch zoom, the caption with chips,
         "S12 · Sam · 8:15pm" linking to the note, and ✕. The note's actions stay on
         its card. With `sequence` (a gallery, 16d) the swipe runs through every
         image of every item, not just the note's. -->
    <DialogRoot
        :open="!!current"
        @update:open="(open) => !open && viewer.close()">
        <DialogPortal>
            <DialogOverlay class="fixed inset-0 z-50 bg-black/95" />
            <DialogContent
                v-if="current"
                class="fixed inset-0 z-50 flex flex-col text-white outline-none pt-safe px-safe"
                @keydown="onKeydown">
                <header class="flex shrink-0 items-center gap-2 px-2 py-1">
                    <DialogTitle class="sr-only">
                        Image {{ index + 1 }} of {{ images.length }} from
                        {{ authorName(current.note.authorMemberId) }}
                    </DialogTitle>
                    <NuxtLink
                        :to="noteHref"
                        replace
                        class="flex min-h-11 min-w-0 items-center truncate rounded px-2 text-sm text-white/80 hover:text-white hover:underline">
                        {{ meta }}
                    </NuxtLink>
                    <span
                        v-if="images.length > 1"
                        class="text-sm tabular-nums text-white/60">
                        {{ index + 1 }} / {{ images.length }}
                    </span>
                    <div class="flex-1" />
                    <DialogClose
                        class="flex size-11 items-center justify-center rounded-md text-white/80 hover:bg-white/10 hover:text-white"
                        aria-label="Close">
                        <X
                            class="size-6"
                            aria-hidden="true" />
                    </DialogClose>
                </header>

                <div
                    class="relative flex min-h-0 flex-1 items-center justify-center"
                    @touchstart.passive="onTouchStart"
                    @touchend.passive="onTouchEnd">
                    <img
                        :key="image.id"
                        :src="src(campaignId, image.id, 'display')"
                        :alt="alt"
                        :width="image.width"
                        :height="image.height"
                        decoding="async"
                        class="max-h-full max-w-full select-none object-contain"
                        style="touch-action: pinch-zoom" />
                    <button
                        v-if="index > 0"
                        type="button"
                        class="absolute left-1 top-1/2 flex size-11 -translate-y-1/2 items-center justify-center rounded-full bg-black/50 text-white hover:bg-black/70"
                        aria-label="Previous image"
                        @click="step(-1)">
                        <ChevronLeft
                            class="size-6"
                            aria-hidden="true" />
                    </button>
                    <button
                        v-if="index < images.length - 1"
                        type="button"
                        class="absolute right-1 top-1/2 flex size-11 -translate-y-1/2 items-center justify-center rounded-full bg-black/50 text-white hover:bg-black/70"
                        aria-label="Next image"
                        @click="step(1)">
                        <ChevronRight
                            class="size-6"
                            aria-hidden="true" />
                    </button>
                </div>

                <DialogDescription
                    v-if="current.note.text"
                    as="div"
                    class="max-h-[30dvh] shrink-0 overflow-y-auto px-4 pt-2 pb-[max(0.5rem,env(safe-area-inset-bottom))] text-sm text-white/90">
                    <SessionNoteMarkdown
                        :campaignId="campaignId"
                        :text="current.note.text" />
                </DialogDescription>
                <DialogDescription
                    v-else
                    class="sr-only">
                    No caption.
                </DialogDescription>
            </DialogContent>
        </DialogPortal>
    </DialogRoot>
</template>

<script setup lang="ts">
    import { ChevronLeft, ChevronRight, X } from "lucide-vue-next";
    import {
        DialogClose,
        DialogContent,
        DialogDescription,
        DialogOverlay,
        DialogPortal,
        DialogRoot,
        DialogTitle,
    } from "reka-ui";
    import type { SessionNote } from "~/utils/api/types";
    import { imageAlt, stepImage, swipeStep } from "~/utils/images";
    import { NOTE_LINK_PARAM } from "~/utils/noteActions";
    import { formatNoteTime } from "~/utils/sessionDates";

    type ViewerItem = { note: SessionNote; sessionNumber?: number };

    const props = defineProps<{
        campaignId: string;
        /** The notes on screen, to find the one `?image=` belongs to. */
        items: ViewerItem[];
        authorName: (memberId: string) => string;
        /** The notes are loaded: an `?image=` none of them has is dropped. */
        ready?: boolean;
        /** Swipe through the images of all `items`, in order (a gallery, 16d). */
        sequence?: boolean;
        /**
         * Which `useImageViewer().open(id, source)` this viewer answers. Unset for a
         * page's list (and deep links); a gallery sets its own, so two viewers on one
         * page never open together.
         */
        source?: string;
    }>();

    const viewer = useImageViewer();
    const src = useImageUrl();

    const mine = computed(() => viewer.source.value === props.source);
    const current = computed(() => {
        const id = viewer.imageId.value;
        return id && mine.value
            ? props.items.find((item) =>
                  item.note.images.some((image) => image.id === id)
              )
            : undefined;
    });
    const images = computed(() =>
        !current.value
            ? []
            : props.sequence
              ? props.items.flatMap((item) => item.note.images)
              : current.value.note.images
    );
    const index = computed(() =>
        Math.max(
            0,
            images.value.findIndex((image) => image.id === viewer.imageId.value)
        )
    );
    const image = computed(() => images.value[index.value]);
    const alt = computed(() =>
        current.value
            ? imageAlt(
                  current.value.note.text,
                  props.authorName(current.value.note.authorMemberId)
              )
            : ""
    );

    // "S12 · Sam · 8:15pm"
    const meta = computed(() => {
        const item = current.value;
        if (!item) return "";
        const parts = [
            props.authorName(item.note.authorMemberId),
            formatNoteTime(item.note.postedAt),
        ];
        return (
            item.sessionNumber ? [`S${item.sessionNumber}`, ...parts] : parts
        ).join(" · ");
    });
    const noteHref = computed(
        () =>
            `/app/campaigns/${encodeURIComponent(props.campaignId)}?${NOTE_LINK_PARAM}=${encodeURIComponent(current.value?.note.id ?? "")}`
    );

    function step(delta: number) {
        const next =
            images.value[stepImage(index.value, images.value.length, delta)];
        if (next && next.id !== image.value?.id) viewer.show(next.id);
    }

    function onKeydown(event: KeyboardEvent) {
        if (event.key === "ArrowLeft") {
            event.preventDefault();
            step(-1);
        } else if (event.key === "ArrowRight") {
            event.preventDefault();
            step(1);
        }
    }

    // Swipe with one finger; a pinch, or a swipe while zoomed in, is the browser's.
    let start: { x: number; y: number } | null = null;
    function onTouchStart(event: TouchEvent) {
        start =
            event.touches.length === 1
                ? { x: event.touches[0].clientX, y: event.touches[0].clientY }
                : null;
    }
    function onTouchEnd(event: TouchEvent) {
        const from = start;
        start = null;
        if (!from || event.changedTouches.length !== 1) return;
        if ((window.visualViewport?.scale ?? 1) > 1.01) return;
        const touch = event.changedTouches[0];
        const direction = swipeStep(
            touch.clientX - from.x,
            touch.clientY - from.y
        );
        if (direction !== 0) step(direction);
    }

    // A link to an image that is not on screen (deleted, hidden, or not loaded).
    watch(
        () => [props.ready, viewer.imageId.value, current.value] as const,
        ([ready, id, item]) => {
            if (ready && id && !item && mine.value) viewer.close();
        },
        { immediate: true }
    );
</script>
