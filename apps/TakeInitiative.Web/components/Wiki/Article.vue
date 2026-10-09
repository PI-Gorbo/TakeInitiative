<template>
    <!-- An entry's article, read (15f, design §4): its blocks in order, as the viewer
         sees them. Secret blocks the viewer cannot see never reach the page. In the UI
         it is the entry's Summary (25d): on the entry page (`entryName` set) it has the
         "Built from N of M notes" line, source chips on its quotes and an empty state
         that points at the Notes tab. The history dialog shows a version bare. -->
    <section
        :aria-label="entryName ? `${entryName}'s summary` : 'Summary'"
        class="flex flex-col gap-2">
        <div
            v-if="entryName && article.blocks.length > 0"
            class="flex items-center gap-2">
            <p class="text-xs text-muted-foreground">{{ builtFrom }}</p>
            <div class="flex-1" />
            <Button
                v-if="canEdit"
                variant="ghost"
                size="sm"
                class="h-11 gap-1 text-muted-foreground md:h-8"
                @click="emit('edit')">
                <Pencil
                    class="size-4"
                    aria-hidden="true" />
                Edit
            </Button>
        </div>

        <!-- The empty summary (25d): what it is, and the two ways to build it. -->
        <div
            v-if="article.blocks.length === 0 && entryName"
            class="flex flex-col gap-2 rounded-lg border border-dashed px-4 py-5">
            <p class="font-medium">No summary yet</p>
            <p class="text-sm text-muted-foreground">
                The summary is the tidy version of what the table knows about
                {{ entryName }}. Build it by adding the best notes, or write it yourself.
            </p>
            <div class="flex flex-wrap gap-2 pt-1">
                <Button
                    variant="outline"
                    class="h-11 gap-1 md:h-9"
                    @click="emit('pickNotes')">
                    <BookPlus
                        class="size-4"
                        aria-hidden="true" />
                    Pick from notes
                </Button>
                <Button
                    v-if="canEdit"
                    variant="outline"
                    class="h-11 gap-1 md:h-9"
                    @click="emit('edit')">
                    <Pencil
                        class="size-4"
                        aria-hidden="true" />
                    Write one
                </Button>
            </div>
        </div>
        <p
            v-else-if="article.blocks.length === 0"
            class="py-2 text-sm text-muted-foreground">
            Nothing written yet.
        </p>
        <div
            v-else
            class="flex flex-col gap-3">
            <WikiArticleBlock
                v-for="block in article.blocks"
                :key="block.id"
                :campaignId="campaignId"
                :block="block"
                :viewerMemberId="viewerMemberId"
                :nameOf="nameOf"
                :highlighted="block.id === highlightedBlockId"
                :sourceChip="!!entryName"
                @openNote="(noteId: string) => emit('openNote', noteId)" />
        </div>
    </section>
</template>

<script setup lang="ts">
    import { BookPlus, Pencil } from "lucide-vue-next";
    import type { Article } from "~/utils/api/types";
    import { builtFromLabel, quotedNoteIds } from "~/utils/article";

    const props = defineProps<{
        campaignId: string;
        article: Article;
        viewerMemberId: string;
        canEdit: boolean;
        nameOf: (memberId: string) => string;
        highlightedBlockId?: string | null;
        /** The entry page (25d): the entry's name, for the empty state. */
        entryName?: string;
        /** The notes the viewer can see that mention the entry, once all are loaded (25d). */
        noteCount?: number | null;
    }>();
    const emit = defineEmits<{
        edit: [];
        /** "Pick from notes": the Notes tab. */
        pickNotes: [];
        /** A quote's source chip: its note, on the Notes tab. */
        openNote: [noteId: string];
    }>();

    const builtFrom = computed(() => builtFromLabel(quotedNoteIds(props.article.blocks).size, props.noteCount ?? null));
</script>
