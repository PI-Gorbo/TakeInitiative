<template>
    <!-- A whole-number field in the combatant sheet (18d): it saves when it changes (blur
         or Enter), keeps the old value on a bad one, and may be blank when `nullable`. -->
    <label class="flex min-w-0 flex-col gap-1 text-sm">
        <span class="text-xs font-medium text-muted-foreground">{{
            label
        }}</span>
        <input
            ref="input"
            v-model="text"
            type="text"
            inputmode="numeric"
            enterkeyhint="done"
            autocomplete="off"
            :placeholder="placeholder"
            :aria-invalid="error ? 'true' : undefined"
            :aria-describedby="error || hint ? `${id}-help` : undefined"
            class="h-11 w-full rounded-md border bg-background px-3 text-base tabular-nums outline-none focus-visible:ring-1 focus-visible:ring-ring md:h-9 md:text-sm"
            @focus="focused = true"
            @blur="commit"
            @keydown.enter.prevent="commit" />
        <span
            v-if="error || hint"
            :id="`${id}-help`"
            :class="[
                'text-xs',
                error ? 'text-destructive-tint' : 'text-muted-foreground',
            ]"
            >{{ error ?? hint }}</span
        >
    </label>
</template>

<script setup lang="ts">
    import { parseWholeNumber } from "~/utils/combat";

    const props = defineProps<{
        label: string;
        min: number;
        max: number;
        /** Blank is allowed and means none (null). */
        nullable?: boolean;
        placeholder?: string;
        hint?: string;
    }>();
    const model = defineModel<number | null>({ required: true });

    const id = useId();
    const input = ref<HTMLInputElement | null>(null);
    const text = ref(model.value?.toString() ?? "");
    const error = ref<string | null>(null);
    const focused = ref(false);

    // A push or a save elsewhere updates the field, unless it is being typed in.
    watch(model, (value) => {
        if (focused.value) return;
        text.value = value?.toString() ?? "";
        error.value = null;
    });

    function commit() {
        const { value, error: problem } = parseWholeNumber(
            text.value,
            props.min,
            props.max
        );
        focused.value = document.activeElement === input.value;
        if (problem) {
            error.value = problem;
            return;
        }
        if (value === null && !props.nullable) {
            error.value = "Type a number.";
            return;
        }
        error.value = null;
        text.value = value?.toString() ?? "";
        if (value !== (model.value ?? null)) model.value = value;
    }

    defineExpose({ focus: () => input.value?.focus() });
</script>
