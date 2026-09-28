<template>
    <!-- One piece of evidence (glossary, 19c): an article block or a session note as its
         snippet, mentions drawn as chips, or a combat as its card. The snippet is cut
         by the API from what the viewer can see. Each link leaves the sheet. -->
    <article
        v-if="evidence.block"
        class="flex flex-col gap-1 border-b px-4 py-3">
        <NoteMarkdown
            :campaignId="campaignId"
            :text="evidence.block.snippet"
            class="text-sm" />
        <p
            class="flex flex-wrap items-center gap-x-2 text-xs text-muted-foreground">
            <span v-if="secretLabel(evidence.block.visibility)">{{
                secretLabel(evidence.block.visibility)
            }}</span>
            <NuxtLink
                :to="blockHref"
                class="inline-flex min-h-11 items-center gap-1 font-medium text-gold hover:underline md:min-h-0">
                <BookOpen
                    class="size-3.5"
                    aria-hidden="true" />
                {{ evidence.block.entryName }}'s article
            </NuxtLink>
        </p>
    </article>

    <article
        v-else-if="evidence.note"
        class="flex flex-col gap-1 border-b px-4 py-3">
        <NoteMarkdown
            :campaignId="campaignId"
            :text="evidence.note.snippet"
            class="text-sm" />
        <p
            class="flex flex-wrap items-center gap-x-2 text-xs text-muted-foreground">
            <NuxtLink
                :to="noteHref"
                class="inline-flex min-h-11 items-center gap-1 font-medium text-gold hover:underline md:min-h-0">
                {{ noteEvidenceLabel(evidence.note, nameOf) }}
            </NuxtLink>
            <span
                v-if="evidence.note.hasImages"
                role="img"
                aria-label="Has images">
                🖼
            </span>
            <span v-if="secretLabel(evidence.note.visibility)">{{
                secretLabel(evidence.note.visibility)
            }}</span>
            <span
                v-if="evidence.note.isHidden"
                class="inline-flex items-center gap-1">
                <EyeOff
                    class="size-3.5"
                    aria-hidden="true" />
                Hidden
            </span>
        </p>
    </article>

    <div
        v-else-if="evidence.combat"
        class="flex flex-col border-b pt-2">
        <p class="px-4 text-xs font-medium text-muted-foreground">
            ⚔ Fought together
        </p>
        <CombatCard
            :campaignId="campaignId"
            :card="evidence.combat.card"
            :sessionNumber="evidence.combat.sessionNumber"
            :directory="directory" />
    </div>
</template>

<script setup lang="ts">
    import { BookOpen, EyeOff } from "lucide-vue-next";
    import type { Evidence } from "~/utils/api/types";
    import { entryHref, secretLabel } from "~/utils/article";
    import { noteEvidenceLabel } from "~/utils/connections";
    import type { EntryDirectory } from "~/utils/entries";
    import { NOTE_LINK_PARAM } from "~/utils/noteActions";
    import { BLOCK_LINK_PARAM } from "~/utils/search";

    const props = defineProps<{
        campaignId: string;
        evidence: Evidence;
        directory: EntryDirectory;
        nameOf: (memberId: string) => string;
    }>();

    // "Gundren's article" opens the block in its article (`?block=`, 17b's scroll).
    const blockHref = computed(() => {
        const block = props.evidence.block;
        return block
            ? {
                  path: entryHref(props.campaignId, block.entryId),
                  query: { [BLOCK_LINK_PARAM]: block.blockId },
              }
            : "";
    });
    // "S14 · Sam" opens the note in the stream (`?note=`).
    const noteHref = computed(() => {
        const note = props.evidence.note;
        return note
            ? {
                  path: `/app/campaigns/${encodeURIComponent(props.campaignId)}`,
                  query: { [NOTE_LINK_PARAM]: note.noteId },
              }
            : "";
    });
</script>
