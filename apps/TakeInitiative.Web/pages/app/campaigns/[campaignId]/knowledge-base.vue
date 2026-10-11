<template>
    <div class="flex h-full w-full flex-col">
        <!-- The Knowledge base (26f): what the ingest CLI has put in the corpus — monsters,
             spells and items — filtered by category and source book, searched, paged by
             `Load more`, and every row a link out to 5etools.

             It is deliberately **not a fourth bottom tab** (design §3a): the tabs are
             Campaign · Wiki · Combat, and a fourth would cost a third of the wiki's touch
             target for something opened rarely. It is reached from the Wiki's toolbar and
             from ⌘K's "Browse all reference material →".

             Nothing here shows 5eTools' content. A row is a name, a category, the parser's
             label, a book and a page; a reader who wants the rules follows the link to the
             people who wrote them. -->
        <div class="relative shrink-0 border-b">
            <PageContainer class="flex flex-col gap-2 px-3 pb-2 pt-3 md:px-4">
                <NuxtLink
                    :to="`/app/campaigns/${encodeURIComponent(campaignId)}/wiki`"
                    class="-ml-2 flex h-11 w-fit items-center gap-1 rounded-md px-2 text-sm text-muted-foreground hover:bg-accent hover:text-accent-foreground md:h-9">
                    <ChevronLeft
                        class="size-4"
                        aria-hidden="true" />
                    Wiki
                </NuxtLink>
                <h1 class="min-w-0 truncate text-xl font-semibold">Knowledge base</h1>
                <div class="relative">
                    <Search
                        class="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground"
                        aria-hidden="true" />
                    <Input
                        v-model="text"
                        type="search"
                        enterkeyhint="search"
                        :maxlength="QUERY_MAX"
                        placeholder="Search reference material"
                        aria-label="Search reference material by name"
                        class="h-11 pl-9 text-base md:h-9 md:text-sm" />
                </div>
            </PageContainer>
            <PageContainer
                v-if="!desktop"
                class="flex flex-col px-2">
                <KnowledgeBaseFilters
                    v-model:category="category"
                    v-model:book="book"
                    layout="chips"
                    :categoryFacets="categoryFacets"
                    :bookFacets="bookFacets" />
            </PageContainer>
            <!-- Searching: a thin bar; the rows that answered the last search stay under it,
                 as ⌘K's do. Emptying the page on every keystroke was SAM-29. -->
            <div
                v-if="searching"
                class="absolute inset-x-0 -bottom-px h-0.5 overflow-hidden"
                role="progressbar"
                aria-label="Searching">
                <div class="h-full w-1/3 animate-search-bar bg-gold" />
            </div>
        </div>

        <!-- At `lg` the filters become a sticky rail to the left of the same capped column. -->
        <PageContainer
            class="px-3 py-3 pb-safe md:px-4 lg:grid lg:grid-cols-[13rem_minmax(0,1fr)] lg:gap-6">
            <KnowledgeBaseFilters
                v-if="desktop"
                v-model:category="category"
                v-model:book="book"
                layout="rail"
                :categoryFacets="categoryFacets"
                :bookFacets="bookFacets" />
            <div
                class="min-w-0"
                :aria-busy="searching">
                <LoadingFallback
                    v-if="!loaded"
                    :isLoading="list.isLoading.value"
                    :isError="list.isError.value"
                    iconSize="2x">
                    <template #loading>
                        <KnowledgeBaseListSkeleton />
                    </template>
                </LoadingFallback>
                <KnowledgeBaseEmpty
                    v-else-if="emptyState"
                    :state="emptyState"
                    @clear="clearFilters" />
                <KnowledgeBaseList
                    v-else-if="items.length > 0"
                    :items="items"
                    :total="total"
                    :providerLabels="providerLabels"
                    :hasMore="!!list.hasNextPage.value"
                    :loadingMore="list.isFetchingNextPage.value"
                    @loadMore="list.fetchNextPage()" />
            </div>
        </PageContainer>
    </div>
</template>

<script setup lang="ts">
    import { useMediaQuery } from "@vueuse/core";
    import { ChevronLeft, Search } from "lucide-vue-next";
    import { QUERY_MAX } from "~/utils/knowledgeBase";

    definePageMeta({
        layout: "campaign",
        requiresAuth: true,
    });
    useHead({ title: "Knowledge base · Wiki" });

    const route = useRoute("app-campaigns-campaignId-knowledge-base");
    const campaignId = computed(() => route.params.campaignId as string);

    // The filters live in the URL, so this view is shareable and survives a reload (26f).
    const {
        category,
        book,
        text,
        clearFilters,
        list,
        items,
        total,
        loaded,
        searching,
        categoryFacets,
        bookFacets,
        providerLabels,
        emptyState,
    } = useKnowledgeBase(() => campaignId.value);

    // The desktop layout from Tailwind's `lg`, as 25f does it. `/app/**` has no SSR, so the
    // first paint already knows the width and only one set of filter controls is ever mounted.
    const desktop = useMediaQuery("(min-width: 1024px)");
</script>
