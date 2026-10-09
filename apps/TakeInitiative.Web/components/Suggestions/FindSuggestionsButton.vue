<template>
    <!-- "✨ Find suggestions" (23d.2): starts the suggestion model on the loose-ends page, and
         then says what it is doing. Hidden when the device setting is Off. A first download
         always goes through the prompt; a model already on this device loads on the tap. -->
    <div
        v-if="state !== 'off'"
        class="flex flex-col gap-2">
        <div class="flex flex-wrap items-center gap-2">
            <Button
                v-if="state === 'idle' || state === 'needsConsent'"
                variant="outline"
                class="h-11 gap-1 border-gold/40 md:h-9"
                @click="find">
                <span aria-hidden="true">✨</span>
                Find suggestions
            </Button>
            <template
                v-else-if="state === 'downloading' || state === 'loading'">
                <p
                    class="min-w-0 flex-1 text-sm"
                    role="status">
                    {{
                        state === "downloading"
                            ? "Downloading the suggestion model"
                            : "Loading the suggestion model"
                    }}
                    · {{ Math.round(progress.loaded / 1e6) }} of
                    {{ formatMegabytes(progress.total) }}
                </p>
                <Button
                    variant="ghost"
                    class="h-11 md:h-9"
                    @click="extractor.cancel()">
                    Cancel
                </Button>
            </template>
            <template v-else-if="state === 'error'">
                <p
                    class="min-w-0 flex-1 text-sm text-destructive-tint"
                    role="alert">
                    {{
                        extractor.error.value ??
                        "The suggestion model could not load."
                    }}
                </p>
                <Button
                    variant="outline"
                    class="h-11 md:h-9"
                    @click="extractor.ensure({ consent: true })">
                    Retry
                </Button>
            </template>
            <template v-else-if="state === 'ready'">
                <p
                    v-if="phase === 'error'"
                    class="min-w-0 flex-1 text-sm text-destructive-tint"
                    role="alert">
                    Could not look for suggestions.
                </p>
                <Button
                    v-if="phase === 'error'"
                    variant="outline"
                    class="h-11 md:h-9"
                    @click="emit('retry')">
                    Retry
                </Button>
                <p
                    v-else
                    class="text-sm text-muted-foreground"
                    role="status">
                    <span aria-hidden="true">✨</span>
                    {{ readyLabel }}
                </p>
            </template>
        </div>
        <div
            v-if="state === 'downloading' || state === 'loading'"
            class="h-1.5 overflow-hidden rounded bg-muted">
            <div
                class="h-full bg-primary transition-[width]"
                :style="{
                    width: `${progress.total ? (100 * progress.loaded) / progress.total : 0}%`,
                }" />
        </div>

        <SuggestionsDownloadPrompt
            v-model:open="prompt"
            @download="extractor.ensure({ consent: true })" />
    </div>
</template>

<script setup lang="ts">
    import type { LooseEndSuggestionsPhase } from "~/composables/useLooseEndSuggestions";
    import { formatMegabytes } from "~/utils/extraction/modelSource";
    import { suggestionCountLabel } from "~/utils/looseEnds";
    import { lookingAtLabel } from "~/utils/suggestions";

    const props = defineProps<{
        phase: LooseEndSuggestionsPhase;
        /** The notes the model reads. */
        noteCount: number;
        /** The ✨ chips on the page. */
        total: number;
    }>();
    const emit = defineEmits<{ retry: [] }>();

    const extractor = useExtractor();
    const state = extractor.state;
    const progress = extractor.progress;
    const prompt = ref(false);

    const readyLabel = computed(() => {
        if (props.phase === "done")
            return props.total === 0
                ? "No suggestions"
                : suggestionCountLabel(props.total);
        return lookingAtLabel(props.noteCount);
    });

    /** A model already on this device loads on the tap; a first download asks first. */
    function find() {
        if (extractor.cached.value) void extractor.ensure({ consent: true });
        else prompt.value = true;
    }
</script>
