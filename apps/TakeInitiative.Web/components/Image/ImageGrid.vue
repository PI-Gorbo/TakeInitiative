<template>
    <!-- A gallery's grid (16d): square `thumb` tiles, three columns on a phone, four
         from `md` and six from `lg`. Every tile is a button (a 44 px or larger target,
         keyboard focusable) that opens the viewer, with the caption as `alt`. -->
    <ul class="grid grid-cols-3 gap-1 md:grid-cols-4 lg:grid-cols-6">
        <li
            v-for="(tile, index) in tiles"
            :key="tile.image.id">
            <button
                type="button"
                class="relative block aspect-square min-h-11 w-full overflow-hidden rounded-md bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
                :aria-label="`Open image ${index + 1} of ${total ?? tiles.length}: ${altOf(tile)}`"
                @click="emit('open', tile.image.id)">
                <img
                    :src="src(campaignId, tile.image.id, 'thumb')"
                    :alt="altOf(tile)"
                    :width="tile.image.width"
                    :height="tile.image.height"
                    loading="lazy"
                    decoding="async"
                    class="size-full object-cover" />
            </button>
        </li>
    </ul>
</template>

<script setup lang="ts">
    import type { GalleryTile } from "~/utils/gallery";
    import { imageAlt } from "~/utils/images";

    const props = defineProps<{
        campaignId: string;
        tiles: GalleryTile[];
        authorName: (memberId: string) => string;
        /** The gallery's whole count, when more tiles than these exist. */
        total?: number;
    }>();
    const emit = defineEmits<{ open: [imageId: string] }>();

    const src = useImageUrl();
    const altOf = (tile: GalleryTile) => imageAlt(tile.note.text, props.authorName(tile.note.authorMemberId));
</script>
