<template>
    <!-- An entry's CONNECTIONS (design §4, §6, 19c): the entries it shares evidence with,
         as the viewer sees them, heaviest first, each a chip `@Tharden (3)` that opens the
         evidence. On a Character its Places come first under "Seen at"; on a Place its
         Characters under "Seen here". Derived on every read and refetched on the pushes
         that can move one (`useCampaignHub`). `[graph ↗]` opens the graph around it (19d). -->
    <section
        :aria-labelledby="`${id}-title`"
        class="flex flex-col gap-1">
        <div class="flex items-center justify-between gap-2">
            <h3
                :id="`${id}-title`"
                class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                Connections
            </h3>
            <NuxtLink
                :to="graphHref(campaignId, entryId)"
                class="inline-flex min-h-11 items-center px-1 text-xs font-medium text-gold hover:underline md:min-h-0"
                :aria-label="`Graph around ${entryName}`">
                [graph ↗]
            </NuxtLink>
        </div>
        <LoadingFallback
            v-if="!connectionsQuery.data.value"
            :isLoading="connectionsQuery.isLoading.value"
            :isError="connectionsQuery.isError.value"
            iconSize="2x"
            class="py-4" />
        <p
            v-else-if="connections.length === 0"
            class="py-2 text-sm text-muted-foreground">
            {{ noConnectionsLabel(entryName) }}
        </p>
        <div
            v-else
            class="flex flex-col gap-2">
            <div
                v-for="group in shown.groups"
                :key="group.key"
                class="flex flex-col gap-1">
                <h4
                    v-if="group.label"
                    class="text-xs text-muted-foreground">
                    {{ group.label }}
                </h4>
                <ul
                    class="flex flex-wrap gap-1.5"
                    :aria-label="group.label ?? 'Connections'">
                    <li
                        v-for="connection in group.connections"
                        :key="connection.entry.id">
                        <button
                            type="button"
                            class="inline-flex h-11 max-w-full items-center gap-1 rounded-md bg-gold/10 px-2.5 text-sm font-medium text-gold hover:bg-gold/20 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring md:h-8"
                            :aria-label="connectionAriaLabel(connection)"
                            aria-haspopup="dialog"
                            @click="openEvidence(connection)">
                            <span aria-hidden="true">{{
                                ENTRY_KIND_ICONS[connection.entry.kind]
                            }}</span>
                            <span class="truncate">{{
                                connection.entry.name
                            }}</span>
                            <span
                                class="text-muted-foreground"
                                aria-hidden="true">
                                ({{ connection.weight }})
                            </span>
                            <span
                                v-if="foughtTogether(connection)"
                                aria-hidden="true"
                                title="Fought together">
                                ⚔
                            </span>
                        </button>
                    </li>
                </ul>
            </div>
            <Button
                v-if="shown.hidden > 0"
                variant="ghost"
                class="h-11 w-fit px-2 text-xs text-muted-foreground md:h-8"
                @click="expanded = true">
                + {{ shown.hidden }} more
            </Button>
        </div>

        <WikiEvidenceSheet
            v-model:open="sheetOpen"
            :campaignId="campaignId"
            :entryId="entryId"
            :entryName="entryName"
            :connection="selected"
            :nameOf="nameOf" />
    </section>
</template>

<script setup lang="ts">
    import { useQuery } from "@tanstack/vue-query";
    import type { EntryConnection, EntryKind } from "~/utils/api/types";
    import {
        connectionAriaLabel,
        foughtTogether,
        groupConnections,
        limitConnectionGroups,
        noConnectionsLabel,
    } from "~/utils/connections";
    import { ENTRY_KIND_ICONS } from "~/utils/entries";
    import { graphHref } from "~/utils/graph";
    import { getEntryConnectionsQuery } from "~/utils/queries/connections";

    const props = defineProps<{
        campaignId: string;
        entryId: string;
        entryName: string;
        entryKind: EntryKind;
        nameOf: (memberId: string) => string;
    }>();
    const id = useId();

    const connectionsQuery = useQuery(
        getEntryConnectionsQuery(
            () => props.campaignId,
            () => props.entryId
        )
    );
    const connections = computed(
        () => connectionsQuery.data.value?.connections ?? []
    );
    const expanded = ref(false);
    const shown = computed(() => {
        const groups = groupConnections(props.entryKind, connections.value);
        return expanded.value
            ? { groups, hidden: 0 }
            : limitConnectionGroups(groups);
    });

    // The sheet keeps the connection it opened with while it closes, so its title does
    // not blank out mid-animation.
    const sheetOpen = ref(false);
    const selected = ref<EntryConnection | null>(null);
    function openEvidence(connection: EntryConnection) {
        selected.value = connection;
        sheetOpen.value = true;
    }

    watch(
        () => props.entryId,
        () => {
            expanded.value = false;
            sheetOpen.value = false;
        }
    );
</script>
