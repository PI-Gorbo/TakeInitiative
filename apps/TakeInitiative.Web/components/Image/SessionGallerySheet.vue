<template>
    <!-- A session's gallery (16d), opened by "🖼 n" on its divider: a bottom sheet on a
         phone and a dialog from `md`, with an `ImageGrid` of the images the viewer can
         see. A tile opens its own `ImageViewer`, whose swipe runs through the whole
         gallery; the stream's viewer stays shut (`useImageViewer`'s source). -->
    <DialogRoot v-model:open="open">
        <DialogPortal>
            <DialogOverlay
                class="fixed inset-0 z-50 bg-black/80 data-[state=closed]:animate-out data-[state=open]:animate-in data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0" />
            <DialogContent
                class="fixed inset-x-0 bottom-0 z-50 flex max-h-[85dvh] flex-col rounded-t-xl border-t bg-background pb-safe shadow-lg outline-none data-[state=closed]:animate-out data-[state=open]:animate-in data-[state=closed]:slide-out-to-bottom data-[state=open]:slide-in-from-bottom md:inset-x-auto md:bottom-auto md:left-1/2 md:top-1/2 md:max-h-[85vh] md:w-full md:max-w-3xl md:-translate-x-1/2 md:-translate-y-1/2 md:rounded-xl md:border md:pb-0 md:data-[state=closed]:slide-out-to-bottom-0 md:data-[state=open]:slide-in-from-bottom-0">
                <header class="flex shrink-0 items-center gap-2 border-b py-1 pl-4 pr-1">
                    <DialogTitle class="min-w-0 flex-1 truncate text-base font-semibold">
                        <span class="text-gold">Session {{ session?.number }}</span>
                        <template v-if="count !== undefined"> · {{ imageCountLabel(count) }}</template>
                    </DialogTitle>
                    <DialogDescription class="sr-only">
                        The images in Session {{ session?.number }} that you can see.
                    </DialogDescription>
                    <DialogClose
                        class="flex size-11 shrink-0 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-accent-foreground"
                        aria-label="Close">
                        <X
                            class="size-5"
                            aria-hidden="true" />
                    </DialogClose>
                </header>

                <div class="min-h-0 flex-1 overflow-y-auto p-2">
                    <LoadingFallback
                        v-if="!galleryQuery.data.value"
                        :isLoading="galleryQuery.isLoading.value"
                        :isError="galleryQuery.isError.value"
                        iconSize="2x"
                        class="py-8" />
                    <p
                        v-else-if="tiles.length === 0"
                        class="px-2 py-8 text-center text-sm text-muted-foreground">
                        No images in this session.
                    </p>
                    <template v-else>
                        <ImageGrid
                            :campaignId="campaignId"
                            :tiles="tiles"
                            :total="count"
                            :authorName="authorName"
                            @open="(id) => imageViewer.open(id, GALLERY_VIEWER_SOURCE)" />
                        <div
                            v-if="galleryQuery.hasNextPage.value"
                            class="flex justify-center pt-2">
                            <Button
                                variant="ghost"
                                class="h-11 text-xs text-muted-foreground md:h-9"
                                :disabled="galleryQuery.isFetchingNextPage.value"
                                @click="galleryQuery.fetchNextPage()">
                                <LoaderCircle
                                    v-if="galleryQuery.isFetchingNextPage.value"
                                    class="size-4 animate-spin"
                                    aria-hidden="true" />
                                Load more
                            </Button>
                        </div>
                    </template>
                </div>

                <!-- Inside the sheet, so it stacks above it and Escape closes it first. -->
                <ImageViewer
                    :campaignId="campaignId"
                    :items="items"
                    :authorName="authorName"
                    :ready="!!galleryQuery.data.value && !galleryQuery.isFetching.value"
                    :source="GALLERY_VIEWER_SOURCE"
                    sequence />
            </DialogContent>
        </DialogPortal>
    </DialogRoot>
</template>

<script setup lang="ts">
    import { useInfiniteQuery } from "@tanstack/vue-query";
    import { LoaderCircle, X } from "lucide-vue-next";
    import {
        DialogClose,
        DialogContent,
        DialogDescription,
        DialogOverlay,
        DialogPortal,
        DialogRoot,
        DialogTitle,
    } from "reka-ui";
    import type { Session } from "~/utils/api/types";
    import {
        GALLERY_VIEWER_SOURCE,
        galleryImageCount,
        galleryNotes,
        galleryTiles,
        imageCountLabel,
    } from "~/utils/gallery";
    import { NOTE_LINK_PARAM } from "~/utils/noteActions";
    import { getSessionImagesQuery } from "~/utils/queries/sessions";

    const props = defineProps<{
        campaignId: string;
        session: Session | null;
        authorName: (memberId: string) => string;
        /** The divider's count, shown until the gallery answers. */
        loadedCount?: number;
    }>();
    const open = defineModel<boolean>("open", { required: true });

    const galleryQuery = useInfiniteQuery(
        getSessionImagesQuery(
            () => props.campaignId,
            () => props.session?.id ?? "",
            () => open.value
        )
    );
    const items = computed(() =>
        galleryNotes(galleryQuery.data.value).map((item) => ({ note: item.note, sessionNumber: item.sessionNumber }))
    );
    const tiles = computed(() => galleryTiles(items.value));
    const count = computed(() => galleryImageCount(galleryQuery.data.value) ?? props.loadedCount);

    const imageViewer = useImageViewer();

    // The viewer's "S12 · Sam · 8:15pm" goes to the note in the stream: the sheet
    // gets out of the way.
    const route = useRoute();
    watch(
        () => route.query[NOTE_LINK_PARAM],
        (noteId) => {
            if (noteId) open.value = false;
        }
    );
</script>
