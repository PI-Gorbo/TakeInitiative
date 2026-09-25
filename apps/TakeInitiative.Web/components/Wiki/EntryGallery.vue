<template>
    <!-- An entry's gallery (16d, design §4): the images whose caption mentions the entry,
         in notes the viewer can see, newest first. The first 12 tiles, then "See all (n)"
         loads the rest in place. Not drawn at all when there are no images. A tile opens
         the gallery's own viewer, which swipes through the whole gallery. -->
    <section
        v-if="tiles.length > 0"
        :aria-labelledby="`${id}-title`"
        class="flex flex-col gap-2">
        <h3
            :id="`${id}-title`"
            class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            Gallery
        </h3>
        <ImageGrid
            :campaignId="campaign.id"
            :tiles="shown"
            :total="count"
            :authorName="authorName"
            @open="(imageId) => imageViewer.open(imageId, GALLERY_VIEWER_SOURCE)" />
        <div
            v-if="canShowMore"
            class="flex justify-center">
            <Button
                variant="ghost"
                class="h-11 text-xs text-muted-foreground md:h-9"
                :disabled="galleryQuery.isFetchingNextPage.value"
                @click="showMore">
                <LoaderCircle
                    v-if="galleryQuery.isFetchingNextPage.value"
                    class="size-4 animate-spin"
                    aria-hidden="true" />
                {{ expanded ? "Load more" : `See all (${count})` }}
            </Button>
        </div>
    </section>
    <!-- Outside the section, so an open image whose last tile went away still closes. -->
    <ImageViewer
        :campaignId="campaign.id"
        :items="items"
        :authorName="authorName"
        :ready="!!galleryQuery.data.value && !galleryQuery.isFetching.value"
        :source="GALLERY_VIEWER_SOURCE"
        sequence />
</template>

<script setup lang="ts">
    import { useInfiniteQuery } from "@tanstack/vue-query";
    import { LoaderCircle } from "lucide-vue-next";
    import type { Campaign } from "~/utils/api/types";
    import {
        ENTRY_GALLERY_PREVIEW,
        GALLERY_VIEWER_SOURCE,
        galleryImageCount,
        galleryNotes,
        galleryTiles,
    } from "~/utils/gallery";
    import { getEntryImagesQuery } from "~/utils/queries/entries";

    const props = defineProps<{
        campaign: Campaign;
        entryId: string;
    }>();

    const id = useId();
    const galleryQuery = useInfiniteQuery(
        getEntryImagesQuery(
            () => props.campaign.id,
            () => props.entryId
        )
    );
    const items = computed(() =>
        galleryNotes(galleryQuery.data.value).map((item) => ({ note: item.note, sessionNumber: item.sessionNumber }))
    );
    const tiles = computed(() => galleryTiles(items.value));
    const count = computed(() => galleryImageCount(galleryQuery.data.value) ?? tiles.value.length);

    const expanded = ref(false);
    watch(
        () => props.entryId,
        () => (expanded.value = false)
    );
    const shown = computed(() => (expanded.value ? tiles.value : tiles.value.slice(0, ENTRY_GALLERY_PREVIEW)));
    const canShowMore = computed(() =>
        expanded.value ? !!galleryQuery.hasNextPage.value : tiles.value.length > ENTRY_GALLERY_PREVIEW || !!galleryQuery.hasNextPage.value
    );
    function showMore() {
        if (expanded.value || tiles.value.length <= ENTRY_GALLERY_PREVIEW) void galleryQuery.fetchNextPage();
        expanded.value = true;
    }

    const usernames = computed(() => new Map(props.campaign.members.map((m) => [m.memberId, m.username])));
    const authorName = (memberId: string) => usernames.value.get(memberId) ?? "Unknown member";

    const imageViewer = useImageViewer();
</script>
