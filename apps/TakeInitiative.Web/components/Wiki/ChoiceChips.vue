<template>
    <!-- One choice from a few, as touch-sized chips (a radio group): an entry's kind,
         visibility and edit access. -->
    <div
        role="radiogroup"
        :aria-label="label"
        class="flex flex-wrap gap-1.5">
        <button
            v-for="option in options"
            :key="option.value"
            type="button"
            role="radio"
            :aria-checked="modelValue === option.value"
            :title="option.hint"
            :class="[
                'flex h-11 items-center gap-1 rounded-full border px-3 text-sm transition-colors md:h-8',
                modelValue === option.value
                    ? 'border-gold/60 bg-gold/15 font-medium text-gold'
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

<script setup lang="ts" generic="T extends string">
    defineProps<{
        label: string;
        options: readonly { value: T; label: string; icon?: string; hint?: string }[];
    }>();
    const modelValue = defineModel<T>({ required: true });
</script>
