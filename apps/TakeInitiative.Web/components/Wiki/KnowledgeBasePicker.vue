<template>
    <!-- Picking a knowledge-base row to link (27d): a search field over 26's corpus, which is
         26f's browse endpoint with its `q` filter and nothing else — one list, one query, no
         second search path to keep in step. The text is debounced because every keystroke would
         otherwise be a request; TanStack cancels the one a newer keystroke replaced through its
         own `signal`.

         A row the entry already links is shown and disabled rather than hidden: the API answers
         a duplicate with a 409, and "it is already there" is more use than a row that silently
         is not in the list. -->
    <div class="flex min-h-0 flex-col gap-3">
        <label class="flex flex-col gap-1.5">
            <span class="text-sm font-medium">Search your knowledge base</span>
            <Input
                v-model="text"
                type="search"
                :maxlength="QUERY_MAX"
                autocomplete="off"
                enterkeyhint="search"
                placeholder="behol"
                class="h-11 text-base md:h-9 md:text-sm" />
        </label>

        <LoadingFallback
            v-if="!loaded"
            :isLoading="list.isLoading.value"
            :isError="list.isError.value"
            iconSize="2x"
            class="py-6" />
        <p
            v-else-if="items.length === 0"
            class="py-6 text-center text-sm text-muted-foreground">
            {{ empty.title }}
            <span class="block">{{ empty.detail }}</span>
        </p>
        <template v-else>
            <p class="text-xs text-muted-foreground">{{ countLine(total, providerLabels) }}</p>
            <ul class="flex min-h-0 flex-col gap-1 overflow-y-auto">
                <li
                    v-for="item in items"
                    :key="`${item.provider}:${item.id}`">
                    <button
                        type="button"
                        :disabled="linked(item) || busy"
                        class="flex min-h-11 w-full items-start gap-3 rounded-md border p-2 text-left enabled:hover:border-gold/40 enabled:hover:bg-accent disabled:opacity-60 focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                        @click="emit('pick', item)">
                        <span
                            class="flex size-9 shrink-0 items-center justify-center overflow-hidden rounded-md bg-muted text-base"
                            aria-hidden="true">
                            {{ CATEGORY_GLYPHS[item.category] }}
                        </span>
                        <span class="min-w-0 flex-1">
                            <span class="block truncate text-sm font-medium">{{ item.name }}</span>
                            <span class="block truncate text-xs text-muted-foreground">{{ rowLine(item) }}</span>
                            <span class="block truncate text-xs text-muted-foreground">{{ rowSource(item) }}</span>
                        </span>
                        <span
                            v-if="linked(item)"
                            class="shrink-0 self-center text-xs text-muted-foreground">
                            Already linked
                        </span>
                    </button>
                </li>
            </ul>
            <Button
                v-if="list.hasNextPage.value"
                type="button"
                variant="outline"
                class="h-11 md:h-9"
                :disabled="list.isFetchingNextPage.value"
                @click="list.fetchNextPage()">
                <LoaderCircle
                    v-if="list.isFetchingNextPage.value"
                    class="animate-spin"
                    aria-hidden="true" />
                Load more
            </Button>
        </template>
    </div>
</template>

<script setup lang="ts">
    import { useInfiniteQuery } from "@tanstack/vue-query";
    import { refDebounced } from "@vueuse/core";
    import { LoaderCircle } from "lucide-vue-next";
    import type { EntryLink, KnowledgeBaseItem } from "~/utils/api/types";
    import {
        CATEGORY_GLYPHS,
        KNOWLEDGE_BASE_EMPTY,
        QUERY_MAX,
        countLine,
        rowLine,
        rowSource,
        type KnowledgeBaseFilters,
    } from "~/utils/knowledgeBase";
    import { linksItem } from "~/utils/links";
    import { getKnowledgeBaseQuery } from "~/utils/queries/knowledgeBase";

    const props = defineProps<{
        campaignId: string;
        /** The entry's links, so a row already on it can say so. */
        links: readonly EntryLink[];
        /** An add is in flight: the rows stop taking taps so one pick is one link. */
        busy?: boolean;
    }>();
    const emit = defineEmits<{ pick: [item: KnowledgeBaseItem] }>();

    const text = ref("");
    const debounced = refDebounced(text, 200);
    const filters = computed<KnowledgeBaseFilters>(() => ({ category: null, book: null, q: debounced.value }));
    const list = useInfiniteQuery(getKnowledgeBaseQuery(() => props.campaignId, filters));

    const pages = computed(() => list.data.value?.pages ?? []);
    const items = computed(() => pages.value.flatMap((page) => page.items));
    const total = computed(() => pages.value[0]?.total ?? 0);
    const loaded = computed(() => !!list.data.value);
    const providerLabels = computed(() => items.value.map((item) => item.providerLabel ?? item.provider));

    // The picker never asks 26f's "is the corpus empty at all?" probe: it is always searching
    // (even an empty field is the unfiltered first page), so a search with no hits is "no match"
    // and no hits with no search is a corpus with nothing in it.
    const empty = computed(() => KNOWLEDGE_BASE_EMPTY[debounced.value.trim() ? "noMatch" : "notIngested"]);

    const linked = (item: KnowledgeBaseItem) => linksItem(props.links, item);
</script>
