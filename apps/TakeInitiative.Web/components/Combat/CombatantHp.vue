<template>
    <!-- A combatant's HP as the viewer may see it (18c.4): `31 / 45` with a bar, a band
         chip, or nothing. The server has already redacted it (18a.5). -->
    <span
        v-if="view.kind === 'exact'"
        class="flex w-20 shrink-0 flex-col items-end gap-1 md:w-24"
        :aria-label="
            view.maxHp != null
                ? `HP ${view.hp} of ${view.maxHp}`
                : `HP ${view.hp}`
        ">
        <span
            class="text-sm tabular-nums"
            aria-hidden="true">
            <span :class="view.band === 'Down' && 'text-destructive-tint'">{{
                view.hp
            }}</span>
            <span
                v-if="view.maxHp != null"
                class="text-muted-foreground">
                / {{ view.maxHp }}</span
            >
        </span>
        <span
            v-if="view.percent != null"
            class="h-1.5 w-full overflow-hidden rounded-full bg-muted"
            aria-hidden="true">
            <span
                :class="[
                    'block h-full rounded-full',
                    BAR_CLASSES[view.band ?? 'Healthy'],
                ]"
                :style="{ width: `${view.percent}%` }" />
        </span>
    </span>
    <span
        v-else-if="view.kind === 'band'"
        :class="[
            'shrink-0 rounded-full border px-2 py-0.5 text-xs font-medium',
            CHIP_CLASSES[view.band],
        ]">
        {{ view.band }}
    </span>
</template>

<script setup lang="ts">
    import type { Combatant, HpBand } from "~/utils/api/types";
    import { hpView } from "~/utils/combat";

    const props = defineProps<{
        combatant: Pick<Combatant, "hp" | "maxHp" | "band">;
    }>();
    const view = computed(() => hpView(props.combatant));

    const BAR_CLASSES: Record<HpBand, string> = {
        Healthy: "bg-success",
        Bloodied: "bg-gold",
        Down: "bg-destructive",
    };
    const CHIP_CLASSES: Record<HpBand, string> = {
        Healthy: "border-success/50 text-success",
        Bloodied: "border-gold/60 text-gold",
        Down: "border-destructive/60 text-destructive-tint",
    };
</script>
