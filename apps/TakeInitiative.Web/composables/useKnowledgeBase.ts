import { useInfiniteQuery, useQuery } from "@tanstack/vue-query";
import type { KnowledgeBaseBookFacet, KnowledgeBaseCategoryFacet, ReferenceCategory } from "~/utils/api/types";
import {
    BOOK_PARAM,
    CATEGORY_PARAM,
    QUERY_PARAM,
    filtersFromQuery,
    isFiltered,
    knowledgeBaseEmptyState,
    type KnowledgeBaseFilters,
} from "~/utils/knowledgeBase";
import { getKnowledgeBaseProbeQuery, getKnowledgeBaseQuery } from "~/utils/queries/knowledgeBase";

/**
 * The Knowledge base page's state (26f): the filters, which live in the URL, the page of rows
 * under them, and which empty state to show when there are none.
 *
 * **The filters live in `?category=&book=&q=`** so a filtered view is shareable and survives
 * a reload, and every change `replace`s the history entry rather than adding one (14e's
 * convention): a reader who filtered four times and then pressed Back means "leave this
 * page", not "undo one chip".
 *
 * The search box is bound to `q` directly. There is no debounce here and there does not need
 * to be one: `router.replace` is synchronous, the query key changes with the text, and
 * TanStack cancels the request a newer keystroke replaced through its own `signal`.
 */
export function useKnowledgeBase(campaignId: () => string) {
    const route = useRoute();
    const router = useRouter();

    const filters = computed<KnowledgeBaseFilters>(() => filtersFromQuery(route.query));

    /** One parameter, replaced in place. An empty value drops it, so the URL stays plain. */
    function setParam(name: string, value: string | undefined) {
        const { [name]: _, ...rest } = route.query;
        void router.replace({ query: value ? { ...rest, [name]: value } : rest });
    }

    const category = computed<ReferenceCategory | null>({
        get: () => filters.value.category,
        set: (value) => setParam(CATEGORY_PARAM, value?.toLowerCase()),
    });
    const book = computed<string | null>({
        get: () => filters.value.book,
        set: (value) => setParam(BOOK_PARAM, value ?? undefined),
    });
    const text = computed<string>({
        get: () => filters.value.q,
        set: (value) => setParam(QUERY_PARAM, value.trim() || undefined),
    });

    /** "Clear filters": back to the plain page, keeping any other parameter the URL holds. */
    function clearFilters() {
        const { [CATEGORY_PARAM]: _c, [BOOK_PARAM]: _b, [QUERY_PARAM]: _q, ...rest } = route.query;
        void router.replace({ query: rest });
    }

    const list = useInfiniteQuery(getKnowledgeBaseQuery(campaignId, filters));

    const pages = computed(() => list.data.value?.pages ?? []);
    const items = computed(() => pages.value.flatMap((page) => page.items));
    const total = computed(() => pages.value[0]?.total ?? 0);
    const loaded = computed(() => !!list.data.value);

    // Facets come from the first page; each drops its own filter, so the number beside a
    // value is what choosing it would show rather than what this page holds.
    const categoryFacets = computed<KnowledgeBaseCategoryFacet[]>(() => pages.value[0]?.facets.categories ?? []);
    const bookFacets = computed<KnowledgeBaseBookFacet[]>(() => pages.value[0]?.facets.books ?? []);
    const providerLabels = computed(() => items.value.map((item) => item.providerLabel ?? item.provider));

    // The corpus-is-empty question, asked only when a filtered page came back with nothing.
    const probe = useQuery(
        getKnowledgeBaseProbeQuery(
            campaignId,
            () => loaded.value && total.value === 0 && isFiltered(filters.value)
        )
    );

    const emptyState = computed(() =>
        loaded.value
            ? knowledgeBaseEmptyState({
                  total: total.value,
                  filters: filters.value,
                  corpusEmpty: probe.data.value,
              })
            : null
    );

    return {
        filters,
        category,
        book,
        text,
        clearFilters,
        list,
        items,
        total,
        loaded,
        categoryFacets,
        bookFacets,
        providerLabels,
        emptyState,
    };
}
