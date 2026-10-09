<template>
    <div class="flex h-full w-full flex-col">
        <!-- The graph (19d, glossary "Graph"): the Wiki's connections as a force-directed
             drawing, as the viewer sees them. `?focus=`, `?depth=` and `?kinds=` hold the
             view, so a reload or a shared link keeps it. A tap on a node selects it; a
             tap on an edge opens its evidence (19c's sheet). -->
        <div
            class="flex shrink-0 flex-col gap-1.5 border-b px-2 py-1.5 md:px-3">
            <div class="flex items-center gap-1">
                <NuxtLink
                    :to="`/app/campaigns/${encodeURIComponent(campaignId)}/wiki`"
                    class="flex size-11 shrink-0 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-accent-foreground md:size-9"
                    aria-label="Back to the Wiki">
                    <ChevronLeft
                        class="size-5"
                        aria-hidden="true" />
                </NuxtLink>
                <h2 class="min-w-0 flex-1 truncate text-base font-semibold">
                    Graph
                    <template v-if="focusName">
                        <span class="font-normal text-muted-foreground">
                            around
                        </span>
                        <NuxtLink
                            :to="entryHref(campaignId, focusNodeId!)"
                            class="text-gold hover:underline">
                            {{ focusName }}
                        </NuxtLink>
                    </template>
                </h2>
                <Button
                    variant="ghost"
                    class="size-11 shrink-0 p-0 text-muted-foreground md:size-9"
                    aria-label="Fit the graph to the screen"
                    title="Fit to screen"
                    :disabled="nodes.length === 0"
                    @click="graphRef?.fit()">
                    <Maximize
                        class="size-5"
                        aria-hidden="true" />
                </Button>
            </div>
            <WikiGraphControls
                v-model:kinds="kinds"
                v-model:depth="depth"
                :focusName="view.focus ? (focusName ?? 'this entry') : null"
                @clearFocus="setFocus(null)" />
            <p
                v-if="graphQuery.data.value?.truncated"
                class="px-1 text-xs text-muted-foreground">
                Showing the 300 most connected entries.
            </p>
        </div>

        <div class="relative min-h-0 flex-1">
            <div
                v-if="focusMissing"
                class="flex flex-col items-center gap-2 px-4 py-12 text-center text-sm text-muted-foreground">
                That entry is not in the Wiki, or you cannot see it.
                <Button
                    variant="outline"
                    class="h-11 md:h-9"
                    @click="setFocus(null)">
                    Show the whole Wiki
                </Button>
            </div>
            <LoadingFallback
                v-else-if="!graphQuery.data.value"
                :isLoading="graphQuery.isLoading.value"
                :isError="graphQuery.isError.value"
                iconSize="2x"
                class="pt-8" />
            <EmptyState
                v-else-if="nodes.length === 0"
                :icon="Waypoints"
                title="No connections yet"
                class="pt-8">
                Two entries connect when a note or a summary block mentions
                both, or when they fight in one combat.
            </EmptyState>
            <WikiConnectionGraph
                v-else
                ref="graphRef"
                v-model:selected="selectedId"
                :nodes="nodes"
                :edges="edges"
                :focusId="focusNodeId"
                @edge="openEdge" />

            <!-- The selected node's bar: above the tab bar and the home indicator. -->
            <div
                v-if="selectedNode"
                role="region"
                :aria-label="`${selectedNode.entry.name} selected`"
                class="absolute inset-x-2 bottom-2 flex items-center gap-2 rounded-lg border bg-background/95 p-2 shadow-lg backdrop-blur md:inset-x-auto md:left-3 md:max-w-lg">
                <span
                    class="flex size-9 shrink-0 items-center justify-center rounded-full text-lg"
                    :style="{
                        backgroundColor: `color-mix(in srgb, ${KIND_COLOURS[selectedNode.entry.kind]} 22%, transparent)`,
                    }"
                    aria-hidden="true">
                    {{ ENTRY_KIND_ICONS[selectedNode.entry.kind] }}
                </span>
                <div class="min-w-0 flex-1">
                    <p class="truncate text-sm font-semibold">
                        {{ selectedNode.entry.name }}
                    </p>
                    <p class="truncate text-xs text-muted-foreground">
                        {{ selectedSummary }}
                    </p>
                </div>
                <Button
                    variant="ghost"
                    class="h-11 shrink-0 px-2 md:h-9"
                    :disabled="selectedNode.entry.id === focusNodeId"
                    @click="setFocus(selectedNode.entry.id)">
                    <LocateFixed
                        class="size-4"
                        aria-hidden="true" />
                    <span class="max-sm:sr-only">Centre here</span>
                </Button>
                <Button
                    as-child
                    class="h-11 shrink-0 px-3 md:h-9">
                    <NuxtLink
                        :to="entryHref(campaignId, selectedNode.entry.id)">
                        Open
                    </NuxtLink>
                </Button>
                <Button
                    variant="ghost"
                    class="size-11 shrink-0 p-0 text-muted-foreground md:size-9"
                    aria-label="Deselect"
                    @click="selectedId = null">
                    <X
                        class="size-4"
                        aria-hidden="true" />
                </Button>
            </div>
        </div>

        <WikiEvidenceSheet
            v-model:open="sheetOpen"
            :campaignId="campaignId"
            :entryId="evidenceFrom?.entry.id ?? ''"
            :entryName="evidenceFrom?.entry.name ?? ''"
            :connection="evidenceConnection"
            :nameOf="memberName" />
    </div>
</template>

<script setup lang="ts">
    import { useQuery } from "@tanstack/vue-query";
    import {
        ChevronLeft,
        LocateFixed,
        Maximize,
        Waypoints,
        X,
    } from "lucide-vue-next";
    import { apiErrorStatus } from "~/utils/apiErrorParser";
    import type {
        EntryConnection,
        EntryKind,
        GraphEdge,
        GraphNode,
    } from "~/utils/api/types";
    import { entryHref } from "~/utils/article";
    import { ENTRY_KIND_ICONS } from "~/utils/entries";
    import {
        GRAPH_FOCUS_PARAM,
        KIND_COLOURS,
        edgeConnection,
        graphViewFromQuery,
        graphViewToQuery,
        neighboursOf,
        nodeSummary,
        totalWeights,
        type GraphDepth,
        type GraphView,
    } from "~/utils/graph";
    import { getCampaignQuery } from "~/utils/queries/campaign";
    import { getConnectionGraphQuery } from "~/utils/queries/connections";

    definePageMeta({
        layout: "campaign",
        requiresAuth: true,
    });
    useHead({ title: "Graph · Wiki" });

    const route = useRoute("app-campaigns-campaignId-wiki-graph");
    const router = useRouter();
    const campaignId = computed(() => route.params.campaignId as string);

    // ── The view, in the URL ─────────────────────────────────────────────────
    const view = computed(() => graphViewFromQuery(route.query));
    function setView(next: GraphView, push = false) {
        const params = graphViewToQuery(next);
        const query: Record<string, string> = {};
        for (const [key, value] of Object.entries(route.query)) {
            if (key in params || typeof value !== "string") continue;
            query[key] = value;
        }
        for (const [key, value] of Object.entries(params)) {
            if (value !== undefined) query[key] = value;
        }
        void (push ? router.push({ query }) : router.replace({ query }));
    }
    const kinds = computed<EntryKind[]>({
        get: () => view.value.kinds,
        set: (value) => setView({ ...view.value, kinds: value }),
    });
    const depth = computed<GraphDepth>({
        get: () => view.value.depth,
        set: (value) => setView({ ...view.value, depth: value }),
    });
    /** A new focus is a new place to be: Back returns to the last one. */
    function setFocus(id: string | null) {
        selectedId.value = null;
        setView(
            { ...view.value, focus: id, depth: id ? view.value.depth : 1 },
            true
        );
    }

    // ── The graph ────────────────────────────────────────────────────────────
    const graphQuery = useQuery(getConnectionGraphQuery(campaignId, view));
    const nodes = computed<readonly GraphNode[]>(
        () => graphQuery.data.value?.nodes ?? []
    );
    const edges = computed<readonly GraphEdge[]>(
        () => graphQuery.data.value?.edges ?? []
    );
    const byId = computed(
        () => new Map(nodes.value.map((n) => [n.entry.id, n]))
    );
    // An unknown, hidden or bad focus is a 404 or 400: say so rather than draw nothing.
    const focusMissing = computed(() => {
        const status = apiErrorStatus(graphQuery.error.value);
        return !!view.value.focus && (status === 404 || status === 400);
    });
    // The focus as the API answered it (a merged id answers for its target).
    const focusNode = computed(() => {
        if (!view.value.focus) return undefined;
        const asked = view.value.focus.toLowerCase();
        return (
            nodes.value.find((n) => n.depth === 0) ??
            nodes.value.find((n) => n.entry.id.toLowerCase() === asked)
        );
    });
    const focusNodeId = computed(() => focusNode.value?.entry.id ?? null);
    const focusName = computed(() => focusNode.value?.entry.name ?? null);

    /** The drawing, for Fit to screen. */
    const graphRef = ref<{ fit: () => void } | null>(null);

    // ── The selected node ────────────────────────────────────────────────────
    const selectedId = ref<string | null>(null);
    const selectedNode = computed(() =>
        selectedId.value ? byId.value.get(selectedId.value) : undefined
    );
    const selectedSummary = computed(() => {
        const node = selectedNode.value;
        if (!node) return "";
        return nodeSummary(
            node,
            neighboursOf(edges.value, node.entry.id).size,
            totalWeights(edges.value).get(node.entry.id) ?? 0
        );
    });
    // A node that leaves the graph (a kind chip, a refetch) is no longer selected.
    watch(byId, (map) => {
        if (selectedId.value && !map.has(selectedId.value))
            selectedId.value = null;
    });

    // ── An edge's evidence ───────────────────────────────────────────────────
    const campaignQuery = useQuery(getCampaignQuery(campaignId));
    const memberName = (memberId: string) =>
        campaignQuery.data.value?.members.find((m) => m.memberId === memberId)
            ?.username ?? "Unknown member";

    const sheetOpen = ref(false);
    const evidenceFrom = ref<GraphNode | null>(null);
    const evidenceConnection = ref<EntryConnection | null>(null);
    /** The sheet reads from the selected node, or the focus, when it is one end. */
    function openEdge(edge: GraphEdge) {
        const anchor = selectedId.value ?? focusNodeId.value;
        const [fromId, toId] =
            anchor === edge.b ? [edge.b, edge.a] : [edge.a, edge.b];
        const from = byId.value.get(fromId);
        const to = byId.value.get(toId);
        if (!from || !to) return;
        evidenceFrom.value = from;
        evidenceConnection.value = edgeConnection(edge, to);
        sheetOpen.value = true;
    }

    // A new focus (Centre here, Clear focus, Back) starts with nothing selected.
    watch(
        () => route.query[GRAPH_FOCUS_PARAM],
        () => (selectedId.value = null)
    );
</script>
