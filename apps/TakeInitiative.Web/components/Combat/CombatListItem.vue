<template>
    <!-- One row of the Combat tab (18c.3): name, "Round 3 · 6 combatants" and the session. -->
    <NuxtLink
        :to="`/app/campaigns/${encodeURIComponent(campaignId)}/combat/${encodeURIComponent(combat.id)}`"
        class="flex min-h-14 items-center gap-3 rounded-md border px-3 py-2 transition-colors hover:bg-accent/50 focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring">
        <span
            :class="[
                'flex size-9 shrink-0 items-center justify-center rounded-md',
                combat.status === 'Active'
                    ? 'bg-gold/15 text-gold'
                    : 'bg-muted text-muted-foreground',
            ]"
            aria-hidden="true">
            <Swords class="size-5" />
        </span>
        <span class="flex min-w-0 flex-1 flex-col">
            <span class="flex min-w-0 items-center gap-2">
                <span class="truncate font-medium">{{ combat.name }}</span>
                <span
                    v-if="combat.status === 'Active'"
                    class="shrink-0 rounded-full bg-gold px-1.5 text-xs font-semibold text-gold-foreground"
                    >Live</span
                >
            </span>
            <span class="truncate text-xs text-muted-foreground">{{
                combatSummaryLine(combat)
            }}</span>
        </span>
        <span
            class="shrink-0 text-xs text-muted-foreground"
            :aria-label="`Session ${combat.sessionNumber}`"
            >S{{ combat.sessionNumber }}</span
        >
    </NuxtLink>
</template>

<script setup lang="ts">
    import { Swords } from "lucide-vue-next";
    import type { CombatSummary } from "~/utils/api/types";
    import { combatSummaryLine } from "~/utils/combat";

    defineProps<{ campaignId: string; combat: CombatSummary }>();
</script>
