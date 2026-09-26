<template>
    <!-- The stream's filters (14e, glossary §1): a chip row under the header that
         stays put while the stream scrolls. It scrolls sideways on a narrow phone. -->
    <div
        role="group"
        aria-label="Filter the session stream"
        class="flex min-w-0 flex-1 items-center gap-1 overflow-x-auto py-1 [scrollbar-width:none]">
        <button
            v-for="option in STREAM_FILTERS"
            :key="option.value"
            type="button"
            :aria-pressed="modelValue === option.value"
            :class="[
                'flex h-11 shrink-0 items-center rounded-full px-3 text-sm transition-colors md:h-8',
                modelValue === option.value
                    ? 'bg-gold/15 font-medium text-gold'
                    : 'text-muted-foreground hover:bg-accent hover:text-accent-foreground',
            ]"
            @click="modelValue = option.value">
            {{ option.label }}
        </button>
    </div>
</template>

<script setup lang="ts">
    import type { SessionStreamFilter } from "~/utils/api/types";
    import { STREAM_FILTERS } from "~/utils/streamFilters";

    const modelValue = defineModel<SessionStreamFilter>({ required: true });
</script>
