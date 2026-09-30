<template>
    <!-- An entry's COMBATS (design §4, 18e.4): the combats in which it, or an entry merged
         into it, is a combatant the viewer can see, newest first, each a card linking to
         its combat. Nothing at all when there are none, since most entries never fight. -->
    <section
        v-if="items.length > 0"
        :aria-labelledby="`${id}-title`"
        class="flex flex-col gap-1">
        <h3
            :id="`${id}-title`"
            class="text-xs font-semibold uppercase tracking-wide text-muted-foreground"
            :class="{ 'sr-only': embedded }">
            Combats
        </h3>
        <ol class="-mx-4 flex flex-col">
            <li
                v-for="item in items"
                :key="item.combat.id">
                <CombatCard
                    :campaignId="campaignId"
                    :card="item.combat"
                    :sessionNumber="item.sessionNumber"
                    :directory="directory" />
            </li>
        </ol>
    </section>
</template>

<script setup lang="ts">
    import { useQuery } from "@tanstack/vue-query";
    import { getEntryCombatsQuery } from "~/utils/queries/combats";
    import { useEntryDirectory } from "~/utils/queries/entries";

    const props = defineProps<{
        campaignId: string;
        entryId: string;
        /** Inside an `EntrySection` (25e), whose row is the visible title. */
        embedded?: boolean;
    }>();
    const id = useId();

    const combatsQuery = useQuery(
        getEntryCombatsQuery(
            () => props.campaignId,
            () => props.entryId
        )
    );
    const items = computed(() => combatsQuery.data.value?.combats ?? []);
    const directory = useEntryDirectory(() => props.campaignId);
</script>
