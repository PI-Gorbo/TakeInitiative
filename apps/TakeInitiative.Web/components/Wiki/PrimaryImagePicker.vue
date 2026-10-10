<template>
    <!-- Choosing an entry's primary image (SAM-12): a bottom sheet on a phone and a dialog
         from `md`, over the entry's own gallery. Only an image everyone can see may be
         chosen; the rest are drawn dimmed with 🔒 rather than left out, so a picture that is
         in the gallery is never silently missing from the picker. -->
    <DialogRoot v-model:open="open">
        <DialogPortal>
            <DialogOverlay
                class="fixed inset-0 z-50 bg-black/80 data-[state=closed]:animate-out data-[state=open]:animate-in data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0" />
            <DialogContent
                class="fixed inset-x-0 bottom-0 z-50 flex max-h-[85dvh] flex-col rounded-t-xl border-t bg-background pb-safe shadow-lg outline-none data-[state=closed]:animate-out data-[state=open]:animate-in data-[state=closed]:slide-out-to-bottom data-[state=open]:slide-in-from-bottom md:inset-x-auto md:bottom-auto md:left-1/2 md:top-1/2 md:max-h-[85vh] md:w-full md:max-w-3xl md:-translate-x-1/2 md:-translate-y-1/2 md:rounded-xl md:border md:pb-0 md:data-[state=closed]:slide-out-to-bottom-0 md:data-[state=open]:slide-in-from-bottom-0">
                <header class="flex shrink-0 items-center gap-2 border-b py-1 pl-4 pr-1">
                    <DialogTitle class="min-w-0 flex-1 truncate text-base font-semibold">
                        Primary image <span class="text-gold">{{ entry.name }}</span>
                    </DialogTitle>
                    <DialogDescription class="sr-only">
                        Pick the image that stands for {{ entry.name }} in lists, search and combat.
                        {{ PRIMARY_IMAGE_MESSAGES.notEveryone }}
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
                        v-else-if="choices.length === 0"
                        class="px-2 py-8 text-center text-sm text-muted-foreground">
                        {{ PRIMARY_IMAGE_MESSAGES.noImages }}
                    </p>
                    <template v-else>
                        <p
                            v-if="!canChoose"
                            class="px-2 pb-2 text-sm text-muted-foreground">
                            {{ PRIMARY_IMAGE_MESSAGES.noneEligible }}
                        </p>
                        <ul class="grid grid-cols-3 gap-1 md:grid-cols-4 lg:grid-cols-6">
                            <li
                                v-for="choice in choices"
                                :key="choice.tile.image.id">
                                <button
                                    type="button"
                                    class="relative block aspect-square min-h-11 w-full overflow-hidden rounded-md bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed"
                                    :class="choice.current && 'ring-2 ring-gold ring-offset-2'"
                                    :disabled="!choice.eligible || busy"
                                    :title="choice.eligible ? undefined : PRIMARY_IMAGE_MESSAGES.notEveryone"
                                    :aria-label="labelOf(choice)"
                                    :aria-pressed="choice.current"
                                    @click="choose(choice.tile.image.id)">
                                    <img
                                        :src="src(campaignId, choice.tile.image.id, 'thumb')"
                                        :alt="altOf(choice.tile)"
                                        :width="choice.tile.image.width"
                                        :height="choice.tile.image.height"
                                        loading="lazy"
                                        decoding="async"
                                        class="size-full object-cover"
                                        :class="!choice.eligible && 'opacity-40'" />
                                    <span
                                        v-if="!choice.eligible"
                                        class="absolute inset-0 flex items-center justify-center text-lg"
                                        aria-hidden="true">
                                        🔒
                                    </span>
                                    <span
                                        v-else-if="choice.current"
                                        class="absolute bottom-1 right-1 flex size-6 items-center justify-center rounded-full bg-gold text-gold-foreground"
                                        aria-hidden="true">
                                        <Check class="size-4" />
                                    </span>
                                </button>
                            </li>
                        </ul>
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

                <footer
                    v-if="entry.primaryImageId"
                    class="shrink-0 border-t p-2">
                    <Button
                        variant="ghost"
                        class="h-11 w-full text-sm text-muted-foreground md:h-9"
                        :disabled="busy"
                        @click="choose(null)">
                        <Trash2
                            class="size-4"
                            aria-hidden="true" />
                        {{ PRIMARY_IMAGE_MESSAGES.remove }}
                    </Button>
                </footer>
            </DialogContent>
        </DialogPortal>
    </DialogRoot>
</template>

<script setup lang="ts">
    import { useInfiniteQuery } from "@tanstack/vue-query";
    import { Check, LoaderCircle, Trash2, X } from "lucide-vue-next";
    import {
        DialogClose,
        DialogContent,
        DialogDescription,
        DialogOverlay,
        DialogPortal,
        DialogRoot,
        DialogTitle,
    } from "reka-ui";
    import { toast } from "vue-sonner";
    import { apiErrorMessage } from "~/utils/apiErrorParser";
    import type { Entry } from "~/utils/api/types";
    import { galleryNotes, galleryTiles, type GalleryTile } from "~/utils/gallery";
    import { imageAlt } from "~/utils/images";
    import {
        hasPrimaryImageChoice,
        primaryImageChoices,
        PRIMARY_IMAGE_MESSAGES,
        type PrimaryImageChoice,
    } from "~/utils/primaryImage";
    import { getEntryImagesQuery, putEntryPrimaryImageMutation } from "~/utils/queries/entries";

    const props = defineProps<{
        campaignId: string;
        entry: Entry;
        authorName: (memberId: string) => string;
    }>();
    const open = defineModel<boolean>("open", { required: true });

    const galleryQuery = useInfiniteQuery(
        getEntryImagesQuery(
            () => props.campaignId,
            () => props.entry.id
        )
    );
    const tiles = computed(() =>
        galleryTiles(
            galleryNotes(galleryQuery.data.value).map((item) => ({
                note: item.note,
                sessionNumber: item.sessionNumber,
            }))
        )
    );
    const choices = computed(() => primaryImageChoices(tiles.value, props.entry.primaryImageId));
    const canChoose = computed(() => hasPrimaryImageChoice(choices.value));

    const src = useImageUrl();
    const altOf = (tile: GalleryTile) => imageAlt(tile.note.text, props.authorName(tile.note.authorMemberId));
    const labelOf = (choice: PrimaryImageChoice) =>
        choice.current
            ? `The primary image: ${altOf(choice.tile)}`
            : `Make this the primary image: ${altOf(choice.tile)}`;

    const mutation = putEntryPrimaryImageMutation();
    const busy = computed(() => mutation.isPending.value);

    async function choose(imageId: string | null) {
        if (busy.value) return;
        try {
            await mutation.mutateAsync({ campaignId: props.campaignId, entryId: props.entry.id, imageId });
            open.value = false;
        } catch (error) {
            toast.error(apiErrorMessage(error, PRIMARY_IMAGE_MESSAGES.failed));
        }
    }
</script>
