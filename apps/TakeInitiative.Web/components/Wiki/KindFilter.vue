<template>
    <!-- The wiki's kind chips (15c, design §4): All plus the six kinds. A chip row that
         scrolls sideways on a narrow phone, like the stream's filters. -->
    <div
        role="group"
        aria-label="Show entries of a kind"
        class="flex min-w-0 flex-1 items-center gap-1 overflow-x-auto py-1 [scrollbar-width:none]">
        <button
            v-for="option in options"
            :key="option.label"
            type="button"
            :aria-pressed="modelValue === option.value"
            :class="[
                'flex h-11 shrink-0 items-center gap-1 rounded-full px-3 text-sm transition-colors md:h-8',
                modelValue === option.value
                    ? 'bg-gold/15 font-medium text-gold'
                    : 'text-muted-foreground hover:bg-accent hover:text-accent-foreground',
            ]"
            @click="modelValue = option.value">
            <span
                v-if="option.icon"
                aria-hidden="true"
                >{{ option.icon }}</span
            >
            {{ option.label }}
        </button>
    </div>
</template>

<script setup lang="ts">
    import type { EntryKind } from "~/utils/api/types";
    import { ENTRY_KINDS } from "~/utils/entries";

    const modelValue = defineModel<EntryKind | null>({ required: true });

    const options: { value: EntryKind | null; label: string; icon?: string }[] = [
        { value: null, label: "All" },
        ...ENTRY_KINDS.map((k) => ({ value: k.value, label: k.label, icon: k.icon })),
    ];
</script>
