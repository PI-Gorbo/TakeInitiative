<template>
    <!-- Link suggestions (19b, glossary): spans of an unlinked note that the entry
         matcher matched. One tap links the span to the entry, keeping the author's
         words. Nothing happens until the author taps. -->
    <div
        v-if="suggestions.length > 0"
        role="group"
        aria-label="Link suggestions"
        class="flex flex-wrap gap-1.5">
        <button
            v-for="suggestion in suggestions"
            :key="`${suggestion.start}-${suggestion.entry.id}`"
            type="button"
            :disabled="disabled"
            class="flex min-h-11 items-center gap-1 rounded-full border border-gold/40 px-3 text-left text-sm hover:bg-gold/10 disabled:opacity-50 md:min-h-8"
            :aria-label="`Link “${suggestion.text}” to ${suggestion.entry.name}`"
            @click="emit('link', suggestion)">
            <span aria-hidden="true">
                Link <strong class="font-semibold">{{ suggestion.text }}</strong> →
                <span class="text-gold">{{ ENTRY_KIND_ICONS[suggestion.entry.kind] }} @{{ suggestion.entry.name }}</span>?
            </span>
        </button>
    </div>
</template>

<script setup lang="ts">
    import type { LinkSuggestion } from "~/utils/api/types";
    import { ENTRY_KIND_ICONS } from "~/utils/entries";

    defineProps<{
        suggestions: readonly LinkSuggestion[];
        /** While a link is being saved. */
        disabled?: boolean;
    }>();
    const emit = defineEmits<{ link: [suggestion: LinkSuggestion] }>();
</script>
