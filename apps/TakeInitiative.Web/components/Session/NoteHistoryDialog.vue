<template>
    <!-- Edit history (glossary §1): every version of a note with its time, oldest first. -->
    <Dialog v-model:open="open">
        <DialogContent class="max-h-[85dvh] max-w-lg">
            <DialogHeader>
                <DialogTitle>Edit history</DialogTitle>
                <DialogDescription>Every version of this note, oldest first.</DialogDescription>
            </DialogHeader>
            <LoadingFallback
                v-if="!historyQuery.data.value"
                :isLoading="historyQuery.isLoading.value"
                :isError="historyQuery.isError.value"
                iconSize="2x" />
            <ol
                v-else
                class="flex flex-col gap-3">
                <li
                    v-for="(version, index) in historyQuery.data.value.versions"
                    :key="version.at"
                    class="rounded-md border p-3">
                    <div class="mb-1 flex flex-wrap items-baseline gap-x-2 text-xs text-muted-foreground">
                        <span class="font-semibold text-foreground">
                            {{ index === 0 ? "Posted" : `Edit ${index}` }}
                        </span>
                        <time
                            :datetime="version.at"
                            :title="formatNoteDateTime(version.at)">
                            {{ formatHistoryTime(version.at) }}
                        </time>
                        <span
                            v-if="version.isRecap"
                            class="font-semibold text-gold"
                            >📜 RECAP</span
                        >
                        <span
                            v-if="index === historyQuery.data.value.versions.length - 1"
                            class="ml-auto">
                            current
                        </span>
                    </div>
                    <!-- Removed images are deleted, so a version shows how many it had (16b). -->
                    <p
                        v-if="version.imageCount > 0"
                        class="text-xs text-muted-foreground">
                        🖼 {{ version.imageCount === 1 ? "1 image" : `${version.imageCount} images` }}
                    </p>
                    <SessionNoteMarkdown
                        v-if="version.text"
                        :campaignId="campaignId"
                        :text="version.text" />
                </li>
            </ol>
        </DialogContent>
    </Dialog>
</template>

<script setup lang="ts">
    import { useQuery } from "@tanstack/vue-query";
    import { getNoteHistoryQuery } from "~/utils/queries/sessions";
    import { formatNoteDateTime } from "~/utils/sessionDates";

    const props = defineProps<{
        campaignId: string;
        noteId: string;
    }>();
    const open = defineModel<boolean>("open", { required: true });

    const historyQuery = useQuery({
        ...getNoteHistoryQuery(
            () => props.campaignId,
            () => props.noteId
        ),
        enabled: open,
    });

    const formatHistoryTime = (iso: string) =>
        new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" }).format(new Date(iso));
</script>
