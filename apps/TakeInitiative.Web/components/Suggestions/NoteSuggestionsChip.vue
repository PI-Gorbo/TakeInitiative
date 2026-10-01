<template>
    <!-- "✨ 3" on the author's own note card (23e.1): only when the suggestion model is ready in
         this tab, and only once it has read the note (the card is in view). A tap opens the
         sheet; nothing is linked or created until a tap in there.
         23f: the card's menu calls `ask()` here for "✨ Find suggestions", which loads the model
         on the tap (the chip alone never downloads), reads this one note, and opens the sheet. -->
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
            :status="status"
            :working="working"
            :canLookAgain="canLookAgain"
            :unsureBelow="unsureBelow"
            @link="acceptMatch"
            @create="startCreate"
            @dismiss="dismiss"
            @lookAgain="lookAgain"
            @edit="edit" />
        <SuggestionsCreateFromSuggestion
            v-if="createOpened"
            v-model:open="creating"
            :campaignId="campaignId"
            :note="note"
            :suggestion="createFrom"
            :model="model" />
        <!-- The first download on this device: "✨ Find suggestions" asks before it starts. -->
        <SuggestionsDownloadPrompt
            v-if="promptOpened"
            v-model:open="prompt"
            @download="loadThenAsk" />
    </span>
</template>

<script setup lang="ts">
    import type { SessionNote } from "~/utils/api/types";
    import { formatMegabytes } from "~/utils/extraction/modelSource";
    import {
        depthLabel,
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

    const extractor = useExtractor();
    const suggest = useNoteSuggestions({
        campaignId: () => props.campaignId,
        note: () => props.note,
        el: toRef(() => props.target),
        enabled: () => props.enabled,
    });
    const { suggestions, dismiss, model, level, phase, baseThreshold } =
        suggest;
    const { accept, busy } = useAcceptSuggestion(() => props.campaignId);

    // Mounted on first open only, so the stream does not hold a sheet per note.
    const sheetOpen = ref(false);
    const sheetOpened = ref(false);
    const creating = ref(false);
    const createOpened = ref(false);
    const createFrom = ref<ModelSuggestion | null>(null);
    const prompt = ref(false);
    const promptOpened = ref(false);

    function openSheet() {
        sheetOpened.value = true;
        sheetOpen.value = true;
    }

    // ── 23f: the author's own ask, from the note's menu ───────────────────────

    /** True while the model is coming up or the worker is reading this note. */
    const working = computed(
        () =>
            extractor.state.value === "downloading" ||
            extractor.state.value === "loading" ||
            phase.value === "working"
    );

    /** Whether the model failed, either to load or on this note. */
    const failed = computed(
        () => extractor.state.value === "error" || phase.value === "error"
    );

    /** One line in the sheet about what the model is doing, or what went wrong. */
    const status = computed<{ text: string; isError?: boolean } | null>(() => {
        const state = extractor.state.value;
        if (state === "downloading" || state === "loading")
            return {
                text: `${state === "downloading" ? "Downloading" : "Loading"} the suggestion model · ${Math.round(extractor.progress.value.loaded / 1e6)} of ${formatMegabytes(extractor.progress.value.total)}`,
            };
        if (state === "error")
            return {
                text:
                    extractor.error.value ??
                    "The suggestion model could not load.",
                isError: true,
            };
        if (phase.value === "error")
            return {
                text: "Could not look for suggestions on this note.",
                isError: true,
            };
        if (phase.value === "working") return { text: "Reading your note…" };
        if (phase.value === "done")
            return { text: depthLabel(level.value, suggestions.value.length) };
        return null;
    });

    /** The footer offers a deeper pass while there is one — or a second go after a failure. */
    const canLookAgain = computed(
        () => suggest.canLookAgain.value || failed.value
    );
    /** Only a deeper pass has spans under the pinned threshold to mark "unsure". */
    const unsureBelow = computed(() =>
        level.value > 0 ? baseThreshold.value : undefined
    );

    /**
     * "✨ Find suggestions" in the note's menu. A model already on this device loads on the tap;
     * a first download goes through the prompt, as on the loose-ends page (23d.2).
     */
    async function ask() {
        if (extractor.state.value === "off") return;
        if (extractor.state.value === "ready") {
            openSheet();
            suggest.ask();
            return;
        }
        if (!extractor.cached.value) {
            promptOpened.value = true;
            prompt.value = true;
            return;
        }
        await loadThenAsk();
    }

    /** Opens the sheet first, so the download or load reports itself there. */
    async function loadThenAsk() {
        openSheet();
        if (await extractor.ensure({ consent: true })) suggest.ask();
    }

    /** The sheet's "Look again": a deeper pass, or the same one again after a failure. */
    function lookAgain() {
        if (extractor.state.value === "error") {
            void loadThenAsk();
            return;
        }
        if (phase.value === "error") suggest.ask();
        else suggest.lookAgain();
    }

    defineExpose({ ask });

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
