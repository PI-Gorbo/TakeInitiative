<template>
    <!-- The kind chips (design §3a): shown under the mention strip only while a Create
         row is chosen. A tap creates the entry with that kind. `mousedown.prevent` keeps
         the text box focused, so the keyboard stays up. -->
    <div
        role="group"
        aria-label="Kind of the new entry"
        class="flex gap-1 overflow-x-auto">
        <button
            v-for="kind in ENTRY_KINDS"
            :key="kind.value"
            type="button"
            :aria-pressed="kind.value === modelValue"
            :class="[
                'flex h-11 shrink-0 items-center gap-1 whitespace-nowrap rounded-full border px-3 text-sm',
                kind.value === modelValue ? 'border-gold bg-gold/15 text-gold' : 'hover:bg-accent/60',
            ]"
            @mousedown.prevent
            @click="emit('pick', kind.value)">
            <span aria-hidden="true">{{ kind.icon }}</span>
            {{ kind.label }}
        </button>
    </div>
</template>

<script setup lang="ts">
    import type { EntryKind } from "~/utils/api/types";
    import { ENTRY_KINDS } from "~/utils/entries";

    defineProps<{ modelValue: EntryKind }>();
    const emit = defineEmits<{ pick: [kind: EntryKind] }>();
</script>
