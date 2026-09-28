<template>
    <!-- Waiting combatants (18c.4): no initiative yet, in the order they were added. The
         next roll places them. Each shows its roll when the viewer may read it. -->
    <section
        v-if="combatants.length > 0"
        aria-labelledby="waiting-heading"
        class="flex flex-col gap-2">
        <div class="flex items-center gap-2">
            <h3
                id="waiting-heading"
                class="flex-1 text-sm font-semibold text-muted-foreground">
                Waiting · {{ combatants.length }}
            </h3>
            <slot name="action" />
        </div>
        <ul class="flex flex-col gap-1">
            <li
                v-for="combatant in combatants"
                :key="combatant.id"
                :class="[
                    'flex min-h-11 items-center gap-3 rounded-md border border-dashed px-3 py-1.5',
                    combatant.hidden && 'opacity-60',
                ]">
                <EyeOff
                    v-if="combatant.hidden"
                    class="size-4 shrink-0 text-muted-foreground"
                    aria-label="Hidden from players" />
                <NuxtLink
                    v-if="combatant.entryId"
                    :to="entryHref(campaignId, combatant.entryId)"
                    class="min-w-0 flex-1 truncate text-sm font-medium underline-offset-2 hover:underline">
                    {{ combatant.name }}
                </NuxtLink>
                <span
                    v-else
                    class="min-w-0 flex-1 truncate text-sm font-medium"
                    >{{ combatant.name }}</span
                >
                <CombatCombatantHp :combatant="combatant" />
                <span
                    v-if="combatant.initiativeRoll"
                    class="shrink-0 rounded bg-muted px-1.5 py-0.5 font-mono text-xs"
                    :aria-label="`Rolls ${combatant.initiativeRoll}`">
                    🎲 {{ combatant.initiativeRoll }}
                </span>
            </li>
        </ul>
    </section>
</template>

<script setup lang="ts">
    import { EyeOff } from "lucide-vue-next";
    import type { Combatant } from "~/utils/api/types";
    import { entryHref } from "~/utils/article";

    defineProps<{ campaignId: string; combatants: Combatant[] }>();
</script>
