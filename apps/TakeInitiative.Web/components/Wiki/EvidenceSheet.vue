<template>
    <!-- One connection's evidence (glossary, 19c): a bottom sheet on a phone and a
         right-hand panel from `md`, titled "Gundren ↔ Tharden (3)". Blocks first, then
         notes newest first, then combats, as the API orders them. The count in the
         title is the rows shown, so it cannot differ from what the viewer reads (19,
         Notes "Weight equals rows"). Any link inside it closes it. -->
    <DialogRoot v-model:open="open">
        <DialogPortal>
            <DialogOverlay
                class="fixed inset-0 z-50 bg-black/80 data-[state=closed]:animate-out data-[state=open]:animate-in data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0" />
            <DialogContent
                class="fixed inset-x-0 bottom-0 z-50 flex max-h-[85dvh] flex-col rounded-t-xl border-t bg-background pb-safe shadow-lg outline-none data-[state=closed]:animate-out data-[state=open]:animate-in data-[state=closed]:slide-out-to-bottom data-[state=open]:slide-in-from-bottom md:inset-y-0 md:left-auto md:right-0 md:max-h-none md:w-full md:max-w-md md:rounded-none md:border-l md:border-t-0 md:pb-0 md:data-[state=closed]:slide-out-to-right md:data-[state=open]:slide-in-from-right md:data-[state=closed]:slide-out-to-bottom-0 md:data-[state=open]:slide-in-from-bottom-0"
                @closeAutoFocus="onCloseAutoFocus">
                <header
                    class="flex shrink-0 items-center gap-2 border-b py-1 pl-4 pr-1">
                    <DialogTitle
                        class="min-w-0 flex-1 truncate text-base font-semibold">
                        {{ entryName }} ↔
                        <NuxtLink
                            v-if="connection"
                            :to="entryHref(campaignId, connection.entry.id)"
                            class="text-gold hover:underline">
                            {{ connection.entry.name }}
                        </NuxtLink>
                        <template v-if="count !== undefined">
                            ({{ count }})</template
                        >
                    </DialogTitle>
                    <DialogDescription class="sr-only">
                        The notes, article blocks and combats you can see that
                        connect {{ entryName }} and
                        {{ connection?.entry.name }}.
                    </DialogDescription>
                    <DialogClose
                        class="flex size-11 shrink-0 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-accent-foreground"
                        aria-label="Close">
                        <X
                            class="size-5"
                            aria-hidden="true" />
                    </DialogClose>
                </header>

                <div class="min-h-0 flex-1 overflow-y-auto">
                    <LoadingFallback
                        v-if="!evidenceQuery.data.value"
                        :isLoading="evidenceQuery.isLoading.value"
                        :isError="evidenceQuery.isError.value"
                        iconSize="2x"
                        class="py-8" />
                    <p
                        v-else-if="rows.length === 0"
                        class="px-4 py-8 text-center text-sm text-muted-foreground">
                        Nothing you can see connects these two any more.
                    </p>
                    <ol v-else>
                        <li
                            v-for="(row, index) in rows"
                            :key="rowKey(row, index)">
                            <WikiEvidenceRow
                                :campaignId="campaignId"
                                :evidence="row"
                                :directory="directory"
                                :nameOf="nameOf" />
                        </li>
                    </ol>
                </div>
            </DialogContent>
        </DialogPortal>
    </DialogRoot>
</template>

<script setup lang="ts">
    import { useQuery } from "@tanstack/vue-query";
    import { X } from "lucide-vue-next";
    import {
        DialogClose,
        DialogContent,
        DialogDescription,
        DialogOverlay,
        DialogPortal,
        DialogRoot,
        DialogTitle,
    } from "reka-ui";
    import type { EntryConnection, Evidence } from "~/utils/api/types";
    import { entryHref } from "~/utils/article";
    import { getConnectionEvidenceQuery } from "~/utils/queries/connections";
    import { useEntryDirectory } from "~/utils/queries/entries";

    const props = defineProps<{
        campaignId: string;
        /** The entry whose page this is: the pair's "from". */
        entryId: string;
        entryName: string;
        /** The chip that opened the sheet. */
        connection: EntryConnection | null;
        nameOf: (memberId: string) => string;
    }>();
    const open = defineModel<boolean>("open", { required: true });

    const evidenceQuery = useQuery(
        getConnectionEvidenceQuery(
            () => props.campaignId,
            () => props.entryId,
            () => props.connection?.entry.id ?? "",
            () => open.value
        )
    );
    const rows = computed(() => evidenceQuery.data.value?.evidence ?? []);
    // The rows once they are in; the chip's weight until then.
    const count = computed(() =>
        evidenceQuery.data.value ? rows.value.length : props.connection?.weight
    );
    const directory = useEntryDirectory(() => props.campaignId);

    const rowKey = (row: Evidence, index: number) =>
        row.block?.blockId ??
        row.note?.noteId ??
        row.combat?.card.id ??
        `${row.kind}-${index}`;

    // A link inside the sheet (a row's, a mention chip, a combat card, the title) moves
    // the route: the sheet gets out of the way and leaves focus where the page put it,
    // so an article block's scroll is not undone by focus going back to the chip.
    const route = useRoute();
    let navigated = false;
    watch(
        () => route.fullPath,
        () => {
            if (!open.value) return;
            navigated = true;
            open.value = false;
        }
    );
    function onCloseAutoFocus(event: Event) {
        if (navigated) event.preventDefault();
        navigated = false;
    }
</script>
