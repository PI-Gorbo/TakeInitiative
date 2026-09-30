<template>
    <div class="flex h-full w-full flex-col">
        <!-- The Wiki tab (design §4, 15c, 25g): a title row with icon buttons (Knowledge
             base, Graph, Sort, New), the search below it, the kind chips, and "Loose ends
             (n)" while the viewer has any (19e). Then every entry the viewer can see. -->
        <div class="shrink-0 border-b">
            <PageContainer class="flex flex-col gap-2 px-3 pb-2 pt-3 md:px-4">
                <div class="flex items-center gap-1">
                    <h1 class="min-w-0 flex-1 truncate text-xl font-semibold">Wiki</h1>
                    <!-- The Knowledge base (26f): the corpus has no bottom tab of its own,
                         so this toolbar and ⌘K are the two ways in. -->
                    <Button
                        as-child
                        variant="ghost"
                        size="icon"
                        class="h-11 w-11 md:h-9 md:w-9">
                        <NuxtLink
                            :to="knowledgeBaseHref(campaignId)"
                            aria-label="Knowledge base"
                            title="Knowledge base">
                            <Library
                                class="size-5 text-muted-foreground md:size-4"
                                aria-hidden="true" />
                        </NuxtLink>
                    </Button>
                    <Button
                        as-child
                        variant="ghost"
                        size="icon"
                        class="h-11 w-11 md:h-9 md:w-9">
                        <NuxtLink
                            :to="graphHref(campaignId)"
                            aria-label="Graph"
                            title="Graph">
                            <Waypoints
                                class="size-5 text-muted-foreground md:size-4"
                                aria-hidden="true" />
                        </NuxtLink>
                    </Button>
                    <DropdownMenu>
                        <DropdownMenuTrigger
                            class="flex size-11 items-center justify-center rounded-md hover:bg-accent hover:text-accent-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring md:size-9"
                            :aria-label="`Sort: ${sortLabel}. Change sort`"
                            :title="`Sort: ${sortLabel}`">
                            <ArrowDownWideNarrow
                                class="size-5 text-muted-foreground md:size-4"
                                aria-hidden="true" />
                        </DropdownMenuTrigger>
                        <DropdownMenuContent
                            align="end"
                            class="w-52">
                            <DropdownMenuLabel>Sort</DropdownMenuLabel>
                            <DropdownMenuRadioGroup
                                :modelValue="sort"
                                @update:modelValue="(v) => (sort = v as WikiSort)">
                                <DropdownMenuRadioItem
                                    v-for="option in WIKI_SORTS"
                                    :key="option.value"
                                    :value="option.value"
                                    class="min-h-11 md:min-h-8">
                                    {{ option.label }}
                                </DropdownMenuRadioItem>
                            </DropdownMenuRadioGroup>
                        </DropdownMenuContent>
                    </DropdownMenu>
                    <Button
                        size="icon"
                        class="ml-1 h-11 w-11 md:h-9 md:w-9"
                        aria-label="New entry"
                        title="New entry"
                        @click="newOpen = true">
                        <Plus
                            class="size-5 md:size-4"
                            aria-hidden="true" />
                    </Button>
                </div>
                <div class="relative">
                    <Search
                        class="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground"
                        aria-hidden="true" />
                    <Input
                        v-model="query"
                        type="search"
                        enterkeyhint="search"
                        placeholder="Filter by name or alias"
                        aria-label="Filter entries by name or alias"
                        class="h-11 pl-9 text-base md:h-9 md:text-sm" />
                </div>
            </PageContainer>
            <PageContainer class="flex items-center gap-2 px-2">
                <WikiKindFilter v-model="kind" />
            </PageContainer>
        </div>
        <NuxtLink
            v-if="looseEndCount > 0"
            :to="looseEndsHref(campaignId)"
            class="block shrink-0 border-b bg-gold/5 text-sm hover:bg-gold/10">
            <PageContainer class="flex min-h-11 items-center gap-2 px-4 md:min-h-9">
                <span aria-hidden="true">🧵</span>
                <span class="flex-1 font-medium">Loose ends ({{ looseEndCount }})</span>
                <ChevronRight
                    class="size-4 text-muted-foreground"
                    aria-hidden="true" />
            </PageContainer>
        </NuxtLink>

        <PageContainer class="flex flex-col gap-3 px-3 py-3 md:px-4">
            <LoadingFallback
                v-if="!entriesQuery.data.value"
                :isLoading="entriesQuery.isLoading.value"
                :isError="entriesQuery.isError.value"
                iconSize="2x"
                class="pt-8" />
            <EmptyState
                v-else-if="entriesQuery.data.value.entries.length === 0"
                :icon="BookOpen"
                title="The Wiki is empty">
                Mention a character, place or faction in a session note, or add one with New entry.
            </EmptyState>
            <p
                v-else-if="shown.length === 0"
                class="py-8 text-center text-sm text-muted-foreground">
                {{ query.trim() ? `No entries match “${query.trim()}”.` : "No entries of this kind yet." }}
            </p>
            <WikiEntryList
                v-else
                :campaignId="campaignId"
                :items="shown"
                :viewerMemberId="campaignQuery.data.value?.currentMemberId"
                :nameOf="memberName" />
        </PageContainer>

        <WikiNewEntryDialog
            v-model:open="newOpen"
            :campaignId="campaignId"
            :initialName="query" />
    </div>
</template>

<script setup lang="ts">
    import { useQuery } from "@tanstack/vue-query";
    import { ArrowDownWideNarrow, BookOpen, ChevronRight, Library, Plus, Search, Waypoints } from "lucide-vue-next";
    import type { EntryKind } from "~/utils/api/types";
    import {
        KIND_PARAM,
        SORT_PARAM,
        WIKI_SORTS,
        filterEntries,
        kindFromQuery,
        kindToQuery,
        sortEntries,
        sortFromQuery,
        sortToQuery,
        type WikiSort,
    } from "~/utils/entries";
    import { graphHref } from "~/utils/graph";
    import { knowledgeBaseHref } from "~/utils/knowledgeBase";
    import { looseEndsHref } from "~/utils/looseEnds";
    import { getCampaignQuery } from "~/utils/queries/campaign";
    import { getEntriesQuery } from "~/utils/queries/entries";
    import { getLooseEndCountsQuery } from "~/utils/queries/looseEnds";

    definePageMeta({
        layout: "campaign",
        requiresAuth: true,
    });

    const route = useRoute("app-campaigns-campaignId-wiki");
    const router = useRouter();
    const campaignId = computed(() => route.params.campaignId as string);

    const entriesQuery = useQuery(getEntriesQuery(campaignId));
    // Member names for "Played by X" (25g); unknown until the campaign loads.
    const campaignQuery = useQuery(getCampaignQuery(campaignId));
    const memberName = (memberId: string) =>
        campaignQuery.data.value?.members.find((m) => m.memberId.toLowerCase() === memberId.toLowerCase())?.username;
    // Loose ends (19e) are the viewer's own and derived; the hub refetches them on pushes.
    const looseEndCountsQuery = useQuery(getLooseEndCountsQuery(campaignId));
    const looseEndCount = computed(() => looseEndCountsQuery.data.value?.total ?? 0);
    // Mention counts and loose ends are never pushed (15b, 19): read them again whenever
    // the tab opens.
    onMounted(() => {
        if (entriesQuery.data.value) void entriesQuery.refetch();
        if (looseEndCountsQuery.data.value) void looseEndCountsQuery.refetch();
    });

    // The kind and the sort live in the URL (`?kind=place&sort=recent`), so a reload
    // or a shared link keeps them. Changing one replaces the history entry.
    function setParam(name: string, value: string | undefined) {
        const { [name]: _, ...rest } = route.query;
        void router.replace({ query: value ? { ...rest, [name]: value } : rest });
    }
    const kind = computed<EntryKind | null>({
        get: () => kindFromQuery(route.query[KIND_PARAM]),
        set: (value) => setParam(KIND_PARAM, kindToQuery(value)),
    });
    const sort = computed<WikiSort>({
        get: () => sortFromQuery(route.query[SORT_PARAM]),
        set: (value) => setParam(SORT_PARAM, sortToQuery(value)),
    });
    const sortLabel = computed(() => WIKI_SORTS.find((s) => s.value === sort.value)?.label ?? "");

    const query = ref("");
    const shown = computed(() =>
        sortEntries(
            filterEntries(entriesQuery.data.value?.entries ?? [], { kind: kind.value, query: query.value }),
            sort.value
        )
    );

    const newOpen = ref(false);
</script>
