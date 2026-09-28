<template>
    <!-- What players see of this combatant (18d.4): Exact "HP and AC", Band "Healthy /
         Bloodied / Down", Nothing "No HP". Arrow keys move between the options. -->
    <div
        role="radiogroup"
        :aria-labelledby="`${id}-label`"
        class="flex flex-col gap-1"
        @keydown="onKeydown">
        <span
            :id="`${id}-label`"
            class="text-xs font-medium text-muted-foreground"
            >Players see</span
        >
        <div class="grid grid-cols-3 gap-1 rounded-md border p-1">
            <button
                v-for="option in PLAYERS_SEE_OPTIONS"
                :key="option.value"
                ref="buttons"
                type="button"
                role="radio"
                :aria-checked="model === option.value"
                :tabindex="model === option.value ? 0 : -1"
                :disabled="disabled"
                :class="[
                    'flex min-h-11 flex-col items-center justify-center rounded px-1 py-1 text-center text-sm leading-tight',
                    model === option.value
                        ? 'bg-gold text-gold-foreground'
                        : 'hover:bg-accent',
                ]"
                @click="model = option.value">
                <span class="font-medium">{{ option.label }}</span>
                <span
                    :class="[
                        'text-[11px]',
                        model === option.value
                            ? 'text-gold-foreground/80'
                            : 'text-muted-foreground',
                    ]"
                    >{{ option.hint }}</span
                >
            </button>
        </div>
    </div>
</template>

<script setup lang="ts">
    import type { PlayersSee } from "~/utils/api/types";
    import { PLAYERS_SEE_OPTIONS } from "~/utils/combat";

    defineProps<{ disabled?: boolean }>();
    const model = defineModel<PlayersSee>({ required: true });
    const id = useId();
    const buttons = ref<HTMLButtonElement[]>([]);

    function onKeydown(event: KeyboardEvent) {
        const by =
            event.key === "ArrowRight" || event.key === "ArrowDown"
                ? 1
                : event.key === "ArrowLeft" || event.key === "ArrowUp"
                  ? -1
                  : 0;
        if (!by) return;
        event.preventDefault();
        const n = PLAYERS_SEE_OPTIONS.length;
        const at = PLAYERS_SEE_OPTIONS.findIndex(
            (o) => o.value === model.value
        );
        const next = (at + by + n) % n;
        model.value = PLAYERS_SEE_OPTIONS[next]!.value;
        buttons.value[next]?.focus();
    }
</script>
