<template>
    <!-- The Wiki tab (design §4, 15c): every entry the viewer can see, by kind, sorted
         by how often or how recently it is mentioned. -->
    <div class="flex h-full w-full flex-col">
        <div class="flex shrink-0 items-center gap-2 border-b px-2">
            <WikiKindFilter v-model="kind" />
        </div>

        <div class="mx-auto flex w-full max-w-5xl flex-col gap-3 px-3 py-3 md:px-4">
            <div class="flex flex-wrap items-center gap-2">
                <div class="relative min-w-0 flex-1 basis-48">
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
                <DropdownMenu>
                    <DropdownMenuTrigger
                        class="flex h-11 items-center gap-1 rounded-md px-2 text-sm hover:bg-accent hover:text-accent-foreground md:h-9"
                        :aria-label="`Sort: ${sortLabel}. Change sort`">
                        <ArrowDownWideNarrow
                            class="size-4 text-muted-foreground"
                            aria-hidden="true" />
                        <span class="font-medium">{{ sortLabel }}</span>
                        <ChevronDown
                            class="size-4 text-muted-foreground"
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
                    class="h-11 gap-1 md:h-9"
                    @click="newOpen = true">
                    <Plus
                        class="size-4"
                        aria-hidden="true" />
                    New entry
                </Button>
            </div>

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
                :items="shown" />
        </div>

        <WikiNewEntryDialog
            v-model:open="newOpen"
            :campaignId="campaignId"
            :initialName="query" />
    </div>
</template>

<script setup lang="ts">
    import { useQuery } from "@tanstack/vue-query";
    import { ArrowDownWideNarrow, BookOpen, ChevronDown, Plus, Search } from "lucide-vue-next";
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
    import { getEntriesQuery } from "~/utils/queries/entries";

    definePageMeta({
        layout: "campaign",
        requiresAuth: true,
    });

    const route = useRoute("app-campaigns-campaignId-wiki");
    const router = useRouter();
    const campaignId = computed(() => route.params.campaignId as string);

    const entriesQuery = useQuery(getEntriesQuery(campaignId));
    // Mention counts are never pushed (15b): read them again whenever the tab opens.
    onMounted(() => {
        if (entriesQuery.data.value) void entriesQuery.refetch();
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
