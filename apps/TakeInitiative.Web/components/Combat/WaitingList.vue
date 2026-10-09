<template>
    <!-- Waiting combatants (18c.4): no initiative yet, in the order they were added. The
         next roll places them. Each shows its roll when the viewer may read it. Tapping one
         the viewer may edit opens its sheet (18d.1), where a typed initiative places it. -->
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
                class="relative flex min-h-11 items-center gap-3 rounded-md border border-dashed px-3 py-1.5">
                <button
                    v-if="canOpen(combatant)"
                    type="button"
                    class="absolute inset-0 rounded-md hover:bg-accent/40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                    :aria-label="`Open ${combatant.name}`"
                    @click="emit('open', combatant.id)" />
                <span
                    :class="[
                        'pointer-events-none relative flex min-w-0 flex-1 items-center gap-3',
                        combatant.hidden && 'opacity-60',
                    ]">
                    <EyeOff
                        v-if="combatant.hidden"
                        class="size-4 shrink-0 text-muted-foreground"
                        aria-label="Hidden from players" />
                    <span class="flex min-w-0 flex-1">
                        <NuxtLink
                            v-if="combatant.entryId"
                            :to="entryHref(campaignId, combatant.entryId)"
                            class="pointer-events-auto min-w-0 truncate text-sm font-medium underline-offset-2 hover:underline">
                            {{ combatant.name }}
                        </NuxtLink>
                        <span
                            v-else
                            class="min-w-0 truncate text-sm font-medium"
                            >{{ combatant.name }}</span
                        >
                    </span>
                    <CombatCombatantHp :combatant="combatant" />
                    <span
                        v-if="combatant.initiativeRoll"
                        class="shrink-0 rounded bg-muted px-1.5 py-0.5 font-mono text-xs"
                        :aria-label="`Rolls ${combatant.initiativeRoll}`">
                        🎲 {{ combatant.initiativeRoll }}
                    </span>
                </span>
            </li>
        </ul>
    </section>
</template>

<script setup lang="ts">
    import { EyeOff } from "lucide-vue-next";
    import type { Combatant } from "~/utils/api/types";
    import { entryHref } from "~/utils/article";

    defineProps<{
        campaignId: string;
        combatants: Combatant[];
        /** Whether the viewer may open a combatant's sheet (18d.1). */
        canOpen: (combatant: Combatant) => boolean;
    }>();
    const emit = defineEmits<{ open: [combatantId: string] }>();
</script>
