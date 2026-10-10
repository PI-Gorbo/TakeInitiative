<template>
    <!-- "✨ 3" on the author's own note card (23e.1): only when the suggestion model is ready in
         this tab, and only once it has read the note (the card is in view). A tap opens the
         sheet; nothing is linked or created until a tap in there.
         23f: the card's menu calls `ask()` here for "✨ Find suggestions", which loads the model
         on the tap (the chip alone never downloads), reads this one note, and opens the sheet.
         SAM-13: every tap is actioned inside the sheet, which stays open and says what landed.
         Only Edit, which hands the note to the composer, still closes it. -->
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
            :model="model"
            :busy="busy"
            :status="status"
            :working="working"
            :canLookAgain="canLookAgain"
            :unsureBelow="unsureBelow"
            :createFor="createFrom"
            @link="acceptMatch"
            @create="startCreate"
            @dismiss="dismiss"
            @cancelCreate="createFrom = null"
            @created="created"
            @lookAgain="lookAgain"
            @edit="edit" />
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
        actionedLabel,
        depthLabel,
        inlineChipAriaLabel,
        modelSuggestionKey,
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
    /** The suggestion whose create form is open in the sheet (SAM-13). */
    const createFrom = ref<ModelSuggestion | null>(null);
    /** What the last tap did, so the sheet says so without closing (SAM-13). */
    const actioned = ref<string | null>(null);
    const prompt = ref(false);
    const promptOpened = ref(false);

    function openSheet() {
        actioned.value = null;
        createFrom.value = null;
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

    /**
     * One line in the sheet about what the model is doing, what went wrong, or — once it has
     * nothing to say — what the author's last tap did (SAM-13).
     */
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
        if (actioned.value) return { text: actioned.value };
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
        actioned.value = null;
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
        if (await accept(props.note, s, s.match.entry.id, model))
            actioned.value = actionedLabel(s.match.entry.name);
    }

    /**
     * "+ Create": the form opens under the chip in the sheet, and a second tap on the same chip
     * closes it again. Nothing is created until its own tap (SAM-13).
     */
    function startCreate(s: ModelSuggestion) {
        const open = createFrom.value;
        createFrom.value =
            open && modelSuggestionKey(open) === modelSuggestionKey(s)
                ? null
                : s;
    }

    /** The form created the entry and linked the span: the sheet stays, and says so. */
    function created(result: { name: string; created: boolean }) {
        createFrom.value = null;
        actioned.value = actionedLabel(result.name, result.created);
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
