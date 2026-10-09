<template>
    <!-- A note's images (16c), from the `thumb` variant. Every <img> has its width and
         height, so nothing shifts when it loads. One image is full width (at most 70vh
         tall); two sit side by side; three are one large and two small; four or more
         are 2 × 2 with "+n" on the last tile. `compact` (an entry's timeline) is a
         strip of up to four thumbnails. A tile opens the viewer. -->
    <div
        v-if="layout.kind !== 'none'"
        :class="[
            compact
                ? 'flex gap-1'
                : layout.kind === 'single'
                  ? 'max-w-xl'
                  : 'grid max-w-md grid-cols-2 gap-1',
            layout.kind === 'trio' && !compact && 'grid-rows-2',
        ]">
        <button
            v-for="(image, index) in shown"
            :key="image.id"
            type="button"
            :class="[
                'relative block overflow-hidden rounded-md bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                compact
                    ? 'size-16 shrink-0'
                    : layout.kind === 'single'
                      ? 'max-w-full'
                      : 'aspect-square w-full',
                !compact &&
                    layout.kind === 'trio' &&
                    index === 0 &&
                    'row-span-2 aspect-auto h-full',
            ]"
            :style="
                !compact && layout.kind === 'single'
                    ? singleStyle(image)
                    : undefined
            "
            :aria-label="
                index === shown.length - 1 && more > 0
                    ? `Open image ${index + 1} of ${images.length}, ${more} more`
                    : `Open image ${index + 1} of ${images.length}`
            "
            @click="emit('open', image.id)">
            <img
                :src="src(campaignId, image.id, 'thumb')"
                :alt="alt"
                :width="image.width"
                :height="image.height"
                loading="lazy"
                decoding="async"
                class="size-full object-cover" />
            <span
                v-if="index === shown.length - 1 && more > 0"
                class="absolute inset-0 flex items-center justify-center bg-black/55 text-lg font-semibold text-white"
                aria-hidden="true">
                +{{ more }}
            </span>
        </button>
    </div>
</template>

<script setup lang="ts">
    import type { NoteImage } from "~/utils/api/types";
    import { imageAlt, imageLayout } from "~/utils/images";

    const props = defineProps<{
        campaignId: string;
        images: NoteImage[];
        /** The caption (the note's text), for `alt`. */
        text: string;
        authorName: string;
        /** An entry timeline's compact card (15c): a strip of up to four. */
        compact?: boolean;
    }>();
    const emit = defineEmits<{ open: [imageId: string] }>();

    const src = useImageUrl();
    const layout = computed(() => imageLayout(props.images.length));
    const shown = computed(() => props.images.slice(0, layout.value.shown));
    const more = computed(() => layout.value.more);
    const alt = computed(() => imageAlt(props.text, props.authorName));

    // Full width, keeping the image's shape, and never taller than 70% of the screen.
    const singleStyle = (image: NoteImage) => ({
        aspectRatio: `${image.width} / ${image.height}`,
        width: `min(100%, calc(70vh * ${image.width / Math.max(1, image.height)}))`,
    });
</script>
