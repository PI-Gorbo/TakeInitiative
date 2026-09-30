<template>
    <!-- "✨ 3" on the author's own note card (23e.1): only when the suggestion model is ready in
         this tab, and only once it has read the note (the card is in view). A tap opens the
         sheet; nothing is linked or created until a tap in there. -->
    <span class="contents">
        <button
            v-if="suggestions.length > 0"
            type="button"
            class="-my-2 flex min-h-11 min-w-11 items-center justify-center gap-0.5 rounded-full px-2 text-xs font-medium text-gold hover:bg-gold/10 md:-my-1 md:min-h-7 md:min-w-0 md:border md:border-dashed md:border-gold/50"
            :aria-label="inlineChipAriaLabel(suggestions.length)"
            aria-haspopup="dialog"
            @click="openSheet">
            <span aria-hidden="true">✨ {{ suggestions.length }}</span>
        </button>
        <SuggestionsNoteSuggestionsSheet
            v-if="sheetOpened"
            v-model:open="sheetOpen"
            :campaignId="campaignId"
            :note="note"
            :suggestions="suggestions"
            :busy="busy"
            @link="acceptMatch"
            @create="startCreate"
            @dismiss="dismiss"
            @edit="edit" />
        <SuggestionsCreateFromSuggestion
            v-if="createOpened"
            v-model:open="creating"
            :campaignId="campaignId"
            :note="note"
            :suggestion="createFrom"
            :model="model" />
    </span>
</template>

<script setup lang="ts">
    import type { SessionNote } from "~/utils/api/types";
    import {
        inlineChipAriaLabel,
        type ModelSuggestion,
    } from "~/utils/suggestions";

    const props = defineProps<{
        campaignId: string;
        note: SessionNote;
        /** The card, watched by the stream's IntersectionObserver. */
        target: HTMLElement | null;
        /** The viewer's own, posted note, in the stream and not being edited. */
        enabled: boolean;
        sessionNumber: number | null;
    }>();

    const { suggestions, dismiss, model } = useNoteSuggestions({
        campaignId: () => props.campaignId,
        note: () => props.note,
        el: toRef(() => props.target),
        enabled: () => props.enabled,
    });
    const { accept, busy } = useAcceptSuggestion(() => props.campaignId);

    // Mounted on first open only, so the stream does not hold a sheet per note.
    const sheetOpen = ref(false);
    const sheetOpened = ref(false);
    const creating = ref(false);
    const createOpened = ref(false);
    const createFrom = ref<ModelSuggestion | null>(null);

    function openSheet() {
        sheetOpened.value = true;
        sheetOpen.value = true;
    }

    /** "✨ Rellan → @Rellan Ashvale?": one tap links it, with the model on the edit (23c). */
    async function acceptMatch(s: ModelSuggestion) {
        if (!s.match || busy.value) return;
        await accept(props.note, s, s.match.entry.id, model);
    }

    /** "+ Create": the sheet makes way for the create dialog; nothing is created until its tap. */
    function startCreate(s: ModelSuggestion) {
        sheetOpen.value = false;
        createFrom.value = s;
        createOpened.value = true;
        creating.value = true;
    }

    /** Finish by hand in the composer, with the `@` picker (step 17). */
    function edit() {
        sheetOpen.value = false;
        useComposerEdit(props.campaignId).start(
            props.note,
            props.sessionNumber
        );
    }
</script>
