<template>
    <!-- A block's visibility (15f): ordinary, 🔒 DM or 🔒 Me, relative to the block's
         owner. Offered only to the owner and the DMs (the API refuses anyone else). -->
    <div
        role="radiogroup"
        :aria-label="`Who sees this ${quote ? 'quote' : 'block'}`"
        class="flex gap-1">
        <button
            v-for="option in options"
            :key="option.value"
            type="button"
            role="radio"
            :aria-checked="modelValue === option.value"
            :title="option.hint"
            :class="[
                'flex h-11 items-center rounded-full border px-2.5 text-xs transition-colors md:h-7',
                modelValue === option.value
                    ? 'border-gold/60 bg-gold/15 font-medium text-gold'
                    : 'text-muted-foreground hover:bg-accent hover:text-accent-foreground',
            ]"
            @mousedown.prevent
            @click="modelValue = option.value">
            {{ option.label }}
        </button>
    </div>
</template>

<script setup lang="ts">
    import type { Visibility } from "~/utils/api/types";
    import { secretAudience } from "~/utils/article";

    const props = defineProps<{
        /** A quote's "ordinary" is the note's `Everyone`. */
        quote?: boolean;
        ownerName: string;
        ownerIsViewer: boolean;
    }>();
    const modelValue = defineModel<Visibility>({ required: true });

    const options = computed(() =>
        (["Everyone", "DM", "Me"] as const).map((value) => ({
            value,
            label: value === "Everyone" ? (props.quote ? "Everyone" : "Ordinary") : `🔒 ${value}`,
            hint: secretAudience(value, props.ownerName, props.ownerIsViewer),
        }))
    );
</script>
