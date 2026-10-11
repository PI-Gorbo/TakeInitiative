import { infiniteQueryOptions, keepPreviousData, queryOptions } from "@tanstack/vue-query";
import type { KnowledgeBase } from "~/utils/api/types";
import {
    KNOWLEDGE_BASE_PAGE_SIZE,
    nextSkip,
    type KnowledgeBaseFilters,
} from "~/utils/knowledgeBase";
import type { RefOrGetter } from "./utils";

/**
 * How long a loaded page of the corpus is treated as fresh. The corpus only changes when an
 * operator runs the ingest, so this can be generous — but it cannot be `Infinity`, or someone
 * who opened the page before the first ingest would go on being told there is nothing in it.
 * The endpoint sends `private, no-cache` for the same reason.
 */
const CORPUS_STALE_TIME = 5 * 60 * 1000;

// The Knowledge base's browse list (26f). The corpus changes only when an operator runs the
// ingest CLI, and the API says so with a day's `Cache-Control`, so nothing here is pushed
// and a loaded page never goes stale on its own.

export const knowledgeBaseKey = (campaignId: MaybeRefOrGetter<string>) => ["knowledgeBase", campaignId];

/**
 * One filtered list, paged by `Load more` rather than by scrolling: a page the reader asked
 * for is a page they can stop asking for, and a corpus of thousands of near-identical rows
 * is the worst possible thing to scroll by accident.
 *
 * The key holds the filters, so going back to a filter combination already loaded shows it
 * at once, with every page the reader had opened still there.
 *
 * The text is part of the key and this page does not debounce, so without `keepPreviousData`
 * every keystroke would blank the list, the count and the facets and collapse the page to a
 * spinner. ⌘K keeps its previous answer on screen for the same reason (`search.ts`).
 */
export const getKnowledgeBaseQuery = (campaignId: RefOrGetter<string>, filters: RefOrGetter<KnowledgeBaseFilters>) =>
    infiniteQueryOptions({
        queryKey: computed(() => {
            const f = toValue(filters);
            return [...knowledgeBaseKey(toValue(campaignId)), "list", f.category ?? "", f.book ?? "", f.q.trim()];
        }),
        queryFn: ({ pageParam, signal }) => {
            const f = toValue(filters);
            return useApi().knowledgeBase.list({
                campaignId: toValue(campaignId),
                q: f.q.trim() || undefined,
                category: f.category ?? undefined,
                book: f.book ?? undefined,
                skip: pageParam,
                take: KNOWLEDGE_BASE_PAGE_SIZE,
                signal,
            });
        },
        initialPageParam: 0,
        // `skip` is how many rows are already on screen: the next page starts where the
        // loaded ones end, and there is no next page once they add up to the total.
        getNextPageParam: (lastPage: KnowledgeBase, pages: KnowledgeBase[]) =>
            nextSkip(
                pages.reduce((count, page) => count + page.items.length, 0),
                lastPage.total
            ),
        enabled: () => !!toValue(campaignId),
        placeholderData: keepPreviousData,
        staleTime: CORPUS_STALE_TIME,
    });

/**
 * Is the corpus empty at all? One row, no filters — the question the two empty states turn
 * on, which a filtered answer cannot settle (`knowledgeBaseEmptyState`). It is only asked
 * when a filtered page came back with nothing, and its key is the campaign's, so the answer
 * is shared by every filter combination that needs it.
 */
export const getKnowledgeBaseProbeQuery = (campaignId: RefOrGetter<string>, enabled: () => boolean) =>
    queryOptions({
        queryKey: computed(() => [...knowledgeBaseKey(toValue(campaignId)), "any"]),
        queryFn: ({ signal }) =>
            useApi().knowledgeBase.list({ campaignId: toValue(campaignId), take: 1, signal }),
        select: (page: KnowledgeBase) => page.total === 0,
        enabled: () => !!toValue(campaignId) && enabled(),
        staleTime: CORPUS_STALE_TIME,
    });
