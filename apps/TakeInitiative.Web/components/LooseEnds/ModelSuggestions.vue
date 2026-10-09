<template>
    <!-- Model suggestions (23d, glossary §1): spans the suggestion model found in the viewer's
         own note, marked ✨. A match links the span on one tap; a new entry opens the create
         dialog. ✕ hides one on this device. Nothing happens until the author taps.
         With `unsureBelow` (23f's deeper passes) a span under that confidence is marked
         "unsure", so a span the automatic pass would have thrown away says so. -->
    <div
        v-if="suggestions.length > 0"
        role="group"
        aria-label="Suggestions"
        class="flex flex-wrap gap-1.5">
        <div
            v-for="s in suggestions"
            :key="`${s.start}-${s.text}`"
            class="flex min-h-11 items-stretch overflow-hidden rounded-full border border-dashed border-gold/50 md:min-h-8">
            <button
                type="button"
                :disabled="disabled"
                class="flex items-center gap-1 pl-3 pr-1 text-left text-sm hover:bg-gold/10 disabled:opacity-50"
                :aria-label="modelSuggestionAriaLabel(s, isUnsureHere(s))"
                @click="s.match ? emit('link', s) : emit('create', s)">
                <span aria-hidden="true">
                    ✨ <strong class="font-semibold">{{ s.text }}</strong>
                    <template v-if="s.match">
                        →
                        <span class="text-gold"
                            >{{ ENTRY_KIND_ICONS[s.match.entry.kind] }} @{{
                                s.match.entry.name
                            }}</span
                        >?
                    </template>
                    <template v-else>
                        looks like {{ kindWithArticle(s.kind) }} ·
                        <span class="text-gold">+ Create</span>
                    </template>
                    <span
                        v-if="isUnsureHere(s)"
                        class="text-muted-foreground"
                        >· unsure</span
                    >
                </span>
            </button>
            <button
                type="button"
                :disabled="disabled"
                class="flex w-11 shrink-0 items-center justify-center text-muted-foreground hover:bg-accent hover:text-accent-foreground disabled:opacity-50 md:w-8"
                :aria-label="`Hide the suggestion “${s.text}” on this device`"
                @click="emit('dismiss', s)">
                <X
                    class="size-4"
                    aria-hidden="true" />
            </button>
        </div>
    </div>
</template>

<script setup lang="ts">
    import { X } from "lucide-vue-next";
    import { ENTRY_KIND_ICONS } from "~/utils/entries";
    import {
        isUnsure,
        kindWithArticle,
        modelSuggestionAriaLabel,
        type ModelSuggestion,
    } from "~/utils/suggestions";

    const props = defineProps<{
        suggestions: readonly ModelSuggestion[];
        /** While a link is being saved. */
        disabled?: boolean;
        /**
         * 23f: the model's pinned threshold, on a note read at a deeper pass. A span under it is
         * marked "unsure". Left out on the automatic pass, where nothing is under it.
         */
        unsureBelow?: number;
    }>();

    const isUnsureHere = (s: ModelSuggestion) =>
        props.unsureBelow !== undefined && isUnsure(s, props.unsureBelow);
    const emit = defineEmits<{
        link: [suggestion: ModelSuggestion];
        create: [suggestion: ModelSuggestion];
        dismiss: [suggestion: ModelSuggestion];
    }>();
</script>
