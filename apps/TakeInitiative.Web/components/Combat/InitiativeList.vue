<template>
    <!-- The initiative order (18c.4): the rolled combatants, highest first, as the server
         sorted them. The turn's row scrolls into view when the turn moves. A DM drags a
         row by its handle to reorder (18d.6), or moves it with the arrow keys on the
         handle; Move up / Move down in the sheet do the same. -->
    <section
        aria-label="Initiative order"
        class="flex flex-col gap-2">
        <ol
            v-if="combatants.length > 0"
            ref="list"
            class="flex flex-col gap-2">
            <CombatCombatantRow
                v-for="combatant in combatants"
                :key="combatant.id"
                :campaignId="campaignId"
                :combatant="combatant"
                :isTurn="combatant.id === turnCombatantId"
                :isDm="isDm"
                :viewerMemberId="viewerMemberId"
                :primaryImageId="primaryImageOf(combatant)"
                :canOpen="canOpen(combatant)"
                :reorderable="reorderable"
                :dragging="drag.draggingId.value === combatant.id"
                :offsetY="drag.offsetY.value"
                :dropLine="dropLine(combatant.id)"
                @open="emit('open', combatant.id)"
                @dragStart="drag.start($event, combatant.id)"
                @move="(by) => move(combatant.id, by)" />
        </ol>
        <p
            v-else
            class="rounded-md border border-dashed px-3 py-6 text-center text-sm text-muted-foreground">
            <slot name="empty">Nobody has rolled initiative yet.</slot>
        </p>
        <p
            class="sr-only"
            aria-live="polite">
            {{ announcement }}
        </p>
    </section>
</template>

<script setup lang="ts">
    import type { Combatant } from "~/utils/api/types";
    import { combatantRowId, dropPosition, moveByOne } from "~/utils/combat";
    import { useEntryDirectory } from "~/utils/queries/entries";

    const props = defineProps<{
        campaignId: string;
        combatants: Combatant[];
        turnCombatantId: string | null | undefined;
        isDm: boolean;
        viewerMemberId: string;
        /** Whether the viewer may open a combatant's sheet. */
        canOpen: (combatant: Combatant) => boolean;
        /** A DM, in a combat that has not finished. */
        reorderable: boolean;
    }>();
    const emit = defineEmits<{
        open: [combatantId: string];
        reorder: [combatantId: string, afterId: string | null];
    }>();

    // A combatant's portrait is its entry's primary image (SAM-12), read from the cached
    // entry directory the wiki already keeps, so combat asks the API for nothing extra. An
    // entry the viewer cannot see is not in the directory, and the row keeps its initiative
    // badge alone.
    const directory = useEntryDirectory(() => props.campaignId);
    const primaryImageOf = (combatant: Combatant) =>
        combatant.entryId
            ? (directory.value.byId.get(combatant.entryId.toLowerCase())?.entry.primaryImageId ?? null)
            : null;

    const list = ref<HTMLElement | null>(null);
    const ids = computed(() => props.combatants.map((c) => c.id));
    const announcement = ref("");

    const drag = useReorderDrag({
        list,
        enabled: () => props.reorderable,
        onDrop: (id, index) => {
            const position = dropPosition(ids.value, id, index);
            if (position) reorder(id, position.afterId);
        },
    });

    function move(id: string, by: -1 | 1) {
        const position = moveByOne(ids.value, id, by);
        if (position) reorder(id, position.afterId);
    }

    function reorder(id: string, afterId: string | null) {
        const name = props.combatants.find((c) => c.id === id)?.name ?? "";
        const after = afterId
            ? props.combatants.find((c) => c.id === afterId)?.name
            : null;
        announcement.value = after
            ? `${name} moved after ${after}.`
            : `${name} moved to the top.`;
        emit("reorder", id, afterId);
    }

    /** The line where a dragged row would land: above a row, or under the last. */
    function dropLine(id: string): "before" | "after" | null {
        const dragged = drag.draggingId.value;
        const index = drag.dropIndex.value;
        if (!dragged || index === null || id === dragged) return null;
        if (!dropPosition(ids.value, dragged, index)) return null; // where it was
        const others = ids.value.filter((x) => x !== dragged);
        if (others[index] === id) return "before";
        if (index === others.length && others.at(-1) === id) return "after";
        return null;
    }

    // Follow the turn. `nearest` leaves the page alone when the row is already in view.
    watch(
        () => props.turnCombatantId,
        async (id, previous) => {
            if (!id || id === previous) return;
            await nextTick();
            const reduce = window.matchMedia?.(
                "(prefers-reduced-motion: reduce)"
            ).matches;
            document.getElementById(combatantRowId(id))?.scrollIntoView({
                block: "nearest",
                behavior: reduce ? "auto" : "smooth",
            });
        }
    );
</script>
