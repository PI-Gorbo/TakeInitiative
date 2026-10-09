import { keepPreviousData, queryOptions } from "@tanstack/vue-query";
import type { SearchResponse, SearchSection, SearchSectionKey } from "~/utils/api/types";
import { SEARCH_MORE_TAKE, searchParams, type SearchInput } from "~/utils/search";
import type { RefOrGetter } from "./utils";

/** A search's answer, with the input it answers, so a kept previous answer knows its own. */
export type SearchResult = SearchInput & { response: SearchResponse };

export const getSearchQueryKey = (campaignId: string, input: SearchInput) => [
    "search",
    campaignId,
    input.scope,
    input.text,
];

/**
 * ⌘K search (17b). Not pushed live: a search is a moment, and reopening the sheet
 * searches again. The previous answer stays on screen while the next one loads, and
 * a newer keystroke cancels a request still in flight (TanStack's `signal`).
 */
export const getSearchQuery = (campaignId: RefOrGetter<string>, input: RefOrGetter<SearchInput>) =>
    queryOptions({
        queryKey: computed(() => getSearchQueryKey(toValue(campaignId), toValue(input))),
        // The key's own values, not the refs': the input may have moved on already.
        queryFn: async ({ queryKey, signal }): Promise<SearchResult> => {
            const [, id, scope, text] = queryKey as [string, string, SearchInput["scope"], string];
            const current: SearchInput = { scope, text };
            const response = await useApi().search.get({ campaignId: id, ...searchParams(current)!, signal });
            return { ...current, response };
        },
        enabled: () => !!toValue(campaignId) && !!searchParams(toValue(input)),
        placeholderData: keepPreviousData,
        staleTime: 0,
        gcTime: 60_000,
        retry: false,
    });

/** "Show more": one section alone, with the server's largest `take`. */
export const getSearchSectionQuery = (campaignId: string, input: SearchInput, section: SearchSectionKey) =>
    queryOptions({
        queryKey: [...getSearchQueryKey(campaignId, input), section],
        queryFn: async ({ signal }): Promise<SearchSection | undefined> => {
            const params = searchParams(input)!;
            const response = await useApi().search.get({
                campaignId,
                q: params.q,
                sections: section.toLowerCase(),
                take: SEARCH_MORE_TAKE,
                signal,
            });
            return response.sections.find((s) => s.key === section);
        },
        staleTime: 0,
        gcTime: 60_000,
        retry: false,
    });
