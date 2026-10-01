// The Knowledge base page's pure rules (26f): the filters as they live in the URL, a row's
// two muted lines, its link out to 5eTools, and which of the two empty states to show.
// `pages/app/campaigns/[campaignId]/knowledge-base.vue` and `components/KnowledgeBase/*`
// only draw these.
//
// The page never shows 5eTools' content (design §11). A row has a name, a category, the
// `label` line the parser built from enums, a source book and page, and — 26g — artwork by
// URL. There is no rules text, no description and no stat block, because the corpus holds
// none: the row's whole purpose is to be findable, linkable (step 27) and to link out.
import type {
    KnowledgeBaseBookFacet,
    KnowledgeBaseCategoryFacet,
    KnowledgeBaseItem,
    ReferenceCategory,
} from "./api/types";

// ── The categories ───────────────────────────────────────────────────────────

/**
 * The three the parser emits, in the order the corpus is sorted by. The glyph is the
 * fallback in a row's artwork slot: it is what a row shows when it has no image, and
 * when 26g's image is null, fails or is blocked.
 */
export const KNOWLEDGE_BASE_CATEGORIES = [
    { value: "Monster", label: "Monsters", glyph: "🐉" },
    { value: "Spell", label: "Spells", glyph: "🔮" },
    { value: "Item", label: "Items", glyph: "🗡️" },
] as const satisfies readonly { value: ReferenceCategory; label: string; glyph: string }[];

export const CATEGORY_GLYPHS = Object.fromEntries(
    KNOWLEDGE_BASE_CATEGORIES.map((c) => [c.value, c.glyph])
) as Record<ReferenceCategory, string>;

/** "Monsters" for the chip, where `category` itself is the row's word ("Monster"). */
export const categoryLabel = (category: ReferenceCategory) =>
    KNOWLEDGE_BASE_CATEGORIES.find((c) => c.value === category)?.label ?? category;

// ── The filters, in the URL ──────────────────────────────────────────────────

export const CATEGORY_PARAM = "category";
export const BOOK_PARAM = "book";
export const QUERY_PARAM = "q";

/** What the page is filtered by. `null` and `""` are "not filtered by that". */
export type KnowledgeBaseFilters = {
    category: ReferenceCategory | null;
    /** A source book's abbreviation, as the facet gives it: `MM`. */
    book: string | null;
    q: string;
};

export const NO_FILTERS: KnowledgeBaseFilters = { category: null, book: null, q: "" };

/** The longest `book` the API accepts (`GetKnowledgeBase.MaxBookLength`). */
export const BOOK_MAX = 40;
/** The longest `q` the API accepts (`SearchQuery.MaxLength`). */
export const QUERY_MAX = 100;

/** The first value of a repeated query parameter, or undefined. */
const one = (value: unknown): string | undefined => {
    const raw = Array.isArray(value) ? value[0] : value;
    return typeof raw === "string" ? raw : undefined;
};

/** `?category=monster` → `Monster`. Missing or unknown values are no filter at all. */
export function categoryFromQuery(value: unknown): ReferenceCategory | null {
    const raw = one(value);
    if (!raw) return null;
    return KNOWLEDGE_BASE_CATEGORIES.find((c) => c.value.toLowerCase() === raw.toLowerCase())?.value ?? null;
}

/**
 * `?book=MM` → `MM`. The value is a source book's abbreviation, which the corpus decides
 * rather than this file, so it is only trimmed and capped at what the API will take. A book
 * that is not in the corpus simply matches nothing, and the "No items match" state says so.
 */
export function bookFromQuery(value: unknown): string | null {
    const raw = one(value)?.trim().slice(0, BOOK_MAX);
    return raw ? raw : null;
}

/** `?q=behol` → `behol`, capped at what the API accepts. */
export function queryTextFromQuery(value: unknown): string {
    return one(value)?.trim().slice(0, QUERY_MAX) ?? "";
}

/** The whole filter state from a route's query. */
export function filtersFromQuery(query: Record<string, unknown>): KnowledgeBaseFilters {
    return {
        category: categoryFromQuery(query[CATEGORY_PARAM]),
        book: bookFromQuery(query[BOOK_PARAM]),
        q: queryTextFromQuery(query[QUERY_PARAM]),
    };
}

/**
 * The filters as query parameters: lower case for the category, as given for the book, and
 * nothing at all for a filter that is not set — so the plain URL stays plain and a
 * shared one carries only what was chosen.
 */
export function filtersToQuery(filters: KnowledgeBaseFilters): Record<string, string> {
    const query: Record<string, string> = {};
    if (filters.category) query[CATEGORY_PARAM] = filters.category.toLowerCase();
    if (filters.book) query[BOOK_PARAM] = filters.book;
    if (filters.q.trim()) query[QUERY_PARAM] = filters.q.trim();
    return query;
}

/** Whether anything is filtered. Both empty states read this. */
export const isFiltered = (filters: KnowledgeBaseFilters) =>
    !!filters.category || !!filters.book || !!filters.q.trim();

/** The page's route, with its filters, for the Wiki toolbar and ⌘K's "Browse all". */
export function knowledgeBaseHref(campaignId: string, filters: KnowledgeBaseFilters = NO_FILTERS): string {
    const path = `/app/campaigns/${encodeURIComponent(campaignId)}/knowledge-base`;
    const query = new URLSearchParams(filtersToQuery(filters)).toString();
    return query ? `${path}?${query}` : path;
}

// ── The filter values ────────────────────────────────────────────────────────

/**
 * The three category chips, in the corpus's own order, each with what choosing it would
 * show. A category the current book and search hold none of counts zero rather than
 * disappearing: the chips are a fixed, memorable row, not a list that reflows as you type.
 */
export function categoryOptions(
    facets: readonly KnowledgeBaseCategoryFacet[]
): { value: ReferenceCategory; label: string; glyph: string; count: number }[] {
    return KNOWLEDGE_BASE_CATEGORIES.map((c) => ({
        ...c,
        count: facets.find((f) => f.category === c.value)?.count ?? 0,
    }));
}

/**
 * The source-book values, as the facets give them. A chosen book the facets do not name —
 * a book with no rows under the current category, or one that is not in the corpus at all —
 * is kept at the front with a count of zero, because a filter you cannot see is a filter you
 * cannot unset.
 */
export function bookOptions(
    facets: readonly KnowledgeBaseBookFacet[],
    selected: string | null
): { value: string; title: string | null; count: number }[] {
    const options = facets.map((f) => ({ value: f.book, title: f.bookTitle ?? null, count: f.count }));
    if (selected && !options.some((o) => o.value === selected)) {
        options.unshift({ value: selected, title: null, count: 0 });
    }
    return options;
}

// ── A row ────────────────────────────────────────────────────────────────────

/**
 * The muted line under the name: `Monster · CR 13 · Large Aberration`.
 *
 * It is the category and then the parser's `label`, whole. **There is no `detail` column
 * and no shorter string**: the plan's table had both a short `label` and a longer `detail`
 * at one point, but the parser emits exactly one string and it already *is* this line, so
 * a second one could only ever have held a copy. A row with no label is just its category.
 */
export const rowLine = (item: Pick<KnowledgeBaseItem, "category" | "label">) =>
    [item.category, item.label].filter(Boolean).join(" · ");

/** The source line: `MM · p. 28`, or just the book where the source states no page. */
export const rowSource = (item: Pick<KnowledgeBaseItem, "book" | "page">) =>
    [item.book, item.page != null ? `p. ${item.page}` : null].filter(Boolean).join(" · ");

/**
 * Where a row goes, and it is the whole interaction: 5etools, in a new tab.
 *
 * There is no in-app detail page for a 5eTools item, because there is nothing we are
 * allowed to put on one — the corpus holds no rules text and no stat block. `noopener`
 * keeps the new tab from reaching back into this one; `noreferrer` keeps our URL, which
 * holds a campaign id, out of their logs.
 */
export const rowLink = (item: Pick<KnowledgeBaseItem, "url">) =>
    ({ href: item.url, target: "_blank", rel: "noopener noreferrer" }) as const;

/** The artwork slot's size in CSS pixels. Fixed, so a row never moves as pictures arrive. */
export const ARTWORK_SIZE = 44;

/**
 * The row's artwork (26g), or null when there is none to draw — the item has no `imageUrl`,
 * or the one it has already failed to load.
 *
 * The bytes are 5eTools', served from their CDN straight to the browser: nothing is fetched
 * or stored on our side, and a reader who blocks it simply keeps the category glyph.
 * `referrerpolicy="no-referrer"` keeps our URL, which carries a campaign id, out of their
 * logs; `loading="lazy"` keeps a long list from being a long list of requests; and the width
 * and height are stated so the slot does not resize when one arrives. `alt` is empty because
 * the picture says nothing the name beside it does not.
 */
export const rowArtwork = (item: Pick<KnowledgeBaseItem, "imageUrl">, failed = false) =>
    item.imageUrl && !failed
        ? ({
              src: item.imageUrl,
              alt: "",
              width: ARTWORK_SIZE,
              height: ARTWORK_SIZE,
              loading: "lazy",
              decoding: "async",
              referrerpolicy: "no-referrer",
          } as const)
        : null;

/**
 * "Opens on 5eTools in a new tab": the row's own words for where the tap goes, read out
 * after the name rather than instead of it, and the ↗ glyph's accessible name.
 */
export const rowLinkHint = (item: Pick<KnowledgeBaseItem, "providerLabel" | "provider">) =>
    `Opens on ${item.providerLabel ?? item.provider} in a new tab`;

/** "2,847 items · 5eTools", the line above the list. A total of one is "1 item". */
export function countLine(total: number, providerLabels: readonly string[] = []): string {
    const items = `${total.toLocaleString("en-US")} ${total === 1 ? "item" : "items"}`;
    return [items, ...new Set(providerLabels)].join(" · ");
}

// ── The two empty states ─────────────────────────────────────────────────────

/**
 * Which empty state the page shows, and the difference between the two is the point of
 * the page: step 21's corpus lived only inside ⌘K, so an unset `IndexPath` made the
 * provider vanish and nothing in the UI was ever responsible for saying "nothing has been
 * ingested yet".
 *
 * - `notIngested` — the corpus is empty. Not an error, and no file path: the reader of
 *   this message is usually not the operator.
 * - `noMatch` — there is a corpus and these filters match none of it.
 */
export type KnowledgeBaseEmpty = "notIngested" | "noMatch";

export const KNOWLEDGE_BASE_EMPTY: Record<KnowledgeBaseEmpty, { title: string; detail: string }> = {
    notIngested: {
        title: "No reference material yet.",
        detail: "An operator ingests it with the knowledge-base CLI.",
    },
    noMatch: { title: "No items match.", detail: "Clear filters." },
};

/**
 * The state for a page that came back with no rows, or null when there are rows to draw.
 *
 * With no filters set the answer is already the whole corpus, so a total of zero *is*
 * "nothing ingested". With filters set the page cannot tell the two apart from its own
 * answer — a filtered query over an empty corpus and one over a full corpus both come back
 * with nothing — so it asks once, unfiltered, and `corpusEmpty` is that answer.
 * `undefined` while that is still in flight: saying "No items match" and then correcting
 * itself would be worse than a moment of nothing.
 */
export function knowledgeBaseEmptyState(state: {
    total: number;
    filters: KnowledgeBaseFilters;
    corpusEmpty?: boolean | undefined;
}): KnowledgeBaseEmpty | null {
    if (state.total > 0) return null;
    if (!isFiltered(state.filters)) return "notIngested";
    if (state.corpusEmpty === undefined) return null;
    return state.corpusEmpty ? "notIngested" : "noMatch";
}

// ── Paging ───────────────────────────────────────────────────────────────────

/** One page of `Load more`. The API caps `take` at 50; this is its own default. */
export const KNOWLEDGE_BASE_PAGE_SIZE = 30;

/** The next page's `skip`, or undefined when everything is loaded (`Load more` goes). */
export function nextSkip(loaded: number, total: number): number | undefined {
    return loaded > 0 && loaded < total ? loaded : undefined;
}
