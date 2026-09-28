<template>
    <!-- One combatant (18c.4): initiative, name (a link to its entry when the viewer can
         see it), HP, AC in a shield and conditions. The turn's row is highlighted. A DM's
         hidden row is dimmed, with what players would see. 18d opens a sheet on tap. -->
    <li
        :id="rowId"
        :aria-current="isTurn ? 'true' : undefined"
        :class="[
            'flex min-h-14 scroll-my-24 items-center gap-3 rounded-md border px-3 py-2 transition-colors',
            isTurn ? 'border-gold bg-gold/10' : 'bg-background',
        ]">
        <span
            :class="[
                'flex size-9 shrink-0 items-center justify-center rounded-md text-sm font-semibold tabular-nums',
                isTurn ? 'bg-gold text-gold-foreground' : 'bg-muted',
            ]"
            :aria-label="
                combatant.initiative != null
                    ? `Initiative ${combatant.initiative}`
                    : 'Waiting'
            ">
            {{ combatant.initiative ?? "–" }}
        </span>

        <span
            :class="[
                'flex min-w-0 flex-1 flex-col gap-1',
                combatant.hidden && 'opacity-60',
            ]">
            <span class="flex min-w-0 items-center gap-2">
                <EyeOff
                    v-if="combatant.hidden"
                    class="size-4 shrink-0 text-muted-foreground"
                    aria-label="Hidden from players" />
                <NuxtLink
                    v-if="combatant.entryId"
                    :to="entryHref(campaignId, combatant.entryId)"
                    class="min-w-0 truncate font-medium underline-offset-2 hover:underline">
                    {{ combatant.name }}
                </NuxtLink>
                <span
                    v-else
                    class="min-w-0 truncate font-medium"
                    >{{ combatant.name }}</span
                >
                <span
                    v-if="isMine"
                    class="shrink-0 rounded border border-gold/50 px-1 text-xs text-gold"
                    >You</span
                >
                <span
                    v-if="isTurn"
                    class="sr-only"
                    >(current turn)</span
                >
            </span>
            <span
                v-if="combatant.hidden && isDm"
                class="text-xs text-muted-foreground">
                Hidden · players would see
                {{ PLAYERS_SEE_LABELS[combatant.playersSee].toLowerCase() }}
            </span>
            <span
                v-else-if="isDm && combatant.playersSee !== 'Exact'"
                class="text-xs text-muted-foreground">
                Players see
                {{ PLAYERS_SEE_LABELS[combatant.playersSee].toLowerCase() }}
            </span>
            <span
                v-if="combatant.conditions.length > 0"
                class="flex flex-wrap gap-1">
                <span
                    v-for="(condition, i) in combatant.conditions"
                    :key="`${condition.label}-${i}`"
                    class="rounded-full bg-accent px-2 py-0.5 text-xs"
                    :title="condition.note ?? undefined">
                    {{ condition.label }}
                </span>
            </span>
        </span>

        <CombatCombatantHp :combatant="combatant" />
        <span
            v-if="combatant.ac != null"
            class="relative flex size-9 shrink-0 items-center justify-center"
            :aria-label="`AC ${combatant.ac}`">
            <Shield
                class="absolute inset-0 size-9 text-muted-foreground"
                aria-hidden="true" />
            <span
                class="relative text-xs font-semibold tabular-nums"
                aria-hidden="true"
                >{{ combatant.ac }}</span
            >
        </span>
    </li>
</template>

<script setup lang="ts">
    import { EyeOff, Shield } from "lucide-vue-next";
    import type { Combatant } from "~/utils/api/types";
    import { entryHref } from "~/utils/article";
    import { PLAYERS_SEE_LABELS, combatantRowId } from "~/utils/combat";

    const props = defineProps<{
        campaignId: string;
        combatant: Combatant;
        isTurn: boolean;
        isDm: boolean;
        viewerMemberId: string;
    }>();

    const rowId = computed(() => combatantRowId(props.combatant.id));
    const isMine = computed(
        () =>
            !!props.combatant.ownerMemberId &&
            props.combatant.ownerMemberId.toLowerCase() ===
                props.viewerMemberId.toLowerCase()
    );
</script>
