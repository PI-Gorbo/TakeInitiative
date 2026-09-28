<template>
    <!-- The initiative order (18c.4): the rolled combatants, highest first, as the server
         sorted them. The turn's row scrolls into view when the turn moves. -->
    <section
        aria-label="Initiative order"
        class="flex flex-col gap-2">
        <ol
            v-if="combatants.length > 0"
            class="flex flex-col gap-2">
            <CombatCombatantRow
                v-for="combatant in combatants"
                :key="combatant.id"
                :campaignId="campaignId"
                :combatant="combatant"
                :isTurn="combatant.id === turnCombatantId"
                :isDm="isDm"
                :viewerMemberId="viewerMemberId" />
        </ol>
        <p
            v-else
            class="rounded-md border border-dashed px-3 py-6 text-center text-sm text-muted-foreground">
            <slot name="empty">Nobody has rolled initiative yet.</slot>
        </p>
    </section>
</template>

<script setup lang="ts">
    import type { Combatant } from "~/utils/api/types";
    import { combatantRowId } from "~/utils/combat";

    const props = defineProps<{
        campaignId: string;
        combatants: Combatant[];
        turnCombatantId: string | null | undefined;
        isDm: boolean;
        viewerMemberId: string;
    }>();

    // Follow the turn. `nearest` leaves the page alone when the row is already in view.
    watch(
        () => props.turnCombatantId,
        async (id, previous) => {
            if (!id || id === previous) return;
            await nextTick();
            const reduce = window.matchMedia?.(
                "(prefers-reduced-motion: reduce)"
            ).matches;
            document
                .getElementById(combatantRowId(id))
                ?.scrollIntoView({
                    block: "nearest",
                    behavior: reduce ? "auto" : "smooth",
                });
        }
    );
</script>
