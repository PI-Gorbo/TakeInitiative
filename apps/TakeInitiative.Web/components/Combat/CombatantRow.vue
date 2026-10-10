<template>
    <!-- One combatant (18c.4): initiative, its entry's primary image when it has one
         (SAM-12), name (a link to its entry when the viewer can see it), HP, AC in a shield
         and conditions. The turn's row is highlighted. A DM's hidden row is dimmed, with what
         players would see. Tapping the row opens its sheet when the viewer may edit it
         (18d.1): a button under the row's content, so the entry link and the drag handle keep
         their own taps. -->
    <li
        :id="rowId"
        :data-reorder-id="combatant.id"
        :aria-current="isTurn ? 'true' : undefined"
        :style="
            dragging ? { transform: `translateY(${offsetY}px)` } : undefined
        "
        :class="[
            'relative flex min-h-14 scroll-my-24 items-center gap-2 rounded-md border py-2 pr-3 md:gap-3',
            reorderable ? 'pl-1' : 'pl-3',
            isTurn ? 'border-gold bg-gold/10' : 'bg-background',
            dragging ? 'z-20 shadow-lg ring-2 ring-gold' : 'transition-colors',
            dropLine === 'before' &&
                'before:absolute before:inset-x-0 before:-top-[5px] before:h-0.5 before:rounded-full before:bg-gold',
            dropLine === 'after' &&
                'after:absolute after:inset-x-0 after:-bottom-[5px] after:h-0.5 after:rounded-full after:bg-gold',
        ]">
        <button
            v-if="canOpen"
            type="button"
            class="absolute inset-0 rounded-md hover:bg-accent/40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            :aria-label="`Open ${combatant.name}`"
            @click="emit('open')" />
        <button
            v-if="reorderable"
            type="button"
            class="relative flex h-11 w-8 shrink-0 cursor-grab touch-manipulation select-none items-center justify-center rounded-md text-muted-foreground [-webkit-touch-callout:none] hover:bg-accent hover:text-accent-foreground active:cursor-grabbing"
            :aria-label="`Reorder ${combatant.name}. Drag, or use the arrow keys.`"
            @pointerdown="emit('dragStart', $event)"
            @keydown.up.prevent="emit('move', -1)"
            @keydown.down.prevent="emit('move', 1)">
            <GripVertical
                class="size-4"
                aria-hidden="true" />
        </button>
        <span
            :class="[
                'pointer-events-none relative flex size-9 shrink-0 items-center justify-center rounded-md text-sm font-semibold tabular-nums',
                isTurn ? 'bg-gold text-gold-foreground' : 'bg-muted',
            ]"
            :aria-label="
                combatant.initiative != null
                    ? `Initiative ${combatant.initiative}`
                    : 'Waiting'
            ">
            {{ combatant.initiative ?? "–" }}
        </span>

        <!-- Only drawn when there is one, so a table of combatants without portraits keeps
             the width it had. -->
        <img
            v-if="primaryImageId && !imageFailed"
            :src="src(campaignId, primaryImageId, 'thumb')"
            alt=""
            aria-hidden="true"
            loading="lazy"
            decoding="async"
            class="pointer-events-none relative size-8 shrink-0 rounded-md bg-muted object-cover"
            :class="combatant.hidden && 'opacity-60'"
            @error="imageFailed = true" />

        <span
            :class="[
                'pointer-events-none relative flex min-w-0 flex-1 flex-col gap-1',
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
                    class="pointer-events-auto min-w-0 truncate font-medium underline-offset-2 hover:underline">
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

        <CombatCombatantHp
            class="pointer-events-none relative"
            :combatant="combatant" />
        <span
            v-if="combatant.ac != null"
            class="pointer-events-none relative flex size-9 shrink-0 items-center justify-center"
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
    import { EyeOff, GripVertical, Shield } from "lucide-vue-next";
    import type { Combatant } from "~/utils/api/types";
    import { entryHref } from "~/utils/article";
    import { PLAYERS_SEE_LABELS, combatantRowId } from "~/utils/combat";

    const props = defineProps<{
        campaignId: string;
        combatant: Combatant;
        isTurn: boolean;
        isDm: boolean;
        viewerMemberId: string;
        /** Its entry's primary image (SAM-12), resolved by the list from the entry directory. */
        primaryImageId?: string | null;
        /** The viewer may open its sheet (18d.1). */
        canOpen?: boolean;
        /** A DM's drag handle (18d.6). */
        reorderable?: boolean;
        dragging?: boolean;
        offsetY?: number;
        /** Where a dragged row would land, drawn as a line. */
        dropLine?: "before" | "after" | null;
    }>();
    const emit = defineEmits<{
        open: [];
        dragStart: [event: PointerEvent];
        move: [by: -1 | 1];
    }>();

    const src = useImageUrl();
    const imageFailed = ref(false);
    watch(
        () => props.primaryImageId,
        () => (imageFailed.value = false)
    );

    const rowId = computed(() => combatantRowId(props.combatant.id));
    const isMine = computed(
        () =>
            !!props.combatant.ownerMemberId &&
            props.combatant.ownerMemberId.toLowerCase() ===
                props.viewerMemberId.toLowerCase()
    );
</script>
