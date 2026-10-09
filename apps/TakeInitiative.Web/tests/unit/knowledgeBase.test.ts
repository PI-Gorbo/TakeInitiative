import { describe, expect, it } from "vitest";
import type { KnowledgeBaseBookFacet, KnowledgeBaseCategoryFacet, KnowledgeBaseItem } from "~/utils/api/types";
import {
    ARTWORK_SIZE,
    BOOK_PARAM,
    CATEGORY_GLYPHS,
    CATEGORY_PARAM,
    KNOWLEDGE_BASE_EMPTY,
    NO_FILTERS,
    QUERY_MAX,
    QUERY_PARAM,
    bookOptions,
    categoryOptions,
    countLine,
    filtersFromQuery,
    filtersToQuery,
    isFiltered,
    knowledgeBaseEmptyState,
    knowledgeBaseHref,
    nextSkip,
    rowArtwork,
    rowLine,
    rowLink,
    rowLinkHint,
    rowSource,
    type KnowledgeBaseFilters,
} from "~/utils/knowledgeBase";

// ── Fixtures ─────────────────────────────────────────────────────────────────

const item = (extra: Partial<KnowledgeBaseItem> = {}): KnowledgeBaseItem => ({
    provider: "5etools",
    providerLabel: "5eTools",
    id: "monster_beholder_mm",
    name: "Beholder",
    category: "Monster",
    label: "CR 13 · Large Aberration",
    book: "MM",
    bookTitle: "Monster Manual (2014)",
    page: 28,
    url: "https://5e.tools/bestiary.html#beholder_mm",
    ...extra,
});

const filters = (extra: Partial<KnowledgeBaseFilters> = {}): KnowledgeBaseFilters => ({
    ...NO_FILTERS,
    ...extra,
});

// ── The filters, in the URL (26f, 14e's convention) ──────────────────────────

describe("the filters live in the URL", () => {
    it("reads a filtered URL back as the same filters", () => {
        expect(filtersFromQuery({ category: "monster", book: "MM", q: "behol" })).toEqual({
            category: "Monster",
            book: "MM",
            q: "behol",
        });
    });

    it("round-trips every filter combination through the query string", () => {
        const combinations: KnowledgeBaseFilters[] = [
            filters(),
            filters({ category: "Spell" }),
            filters({ book: "XGE" }),
            filters({ q: "fireball" }),
            filters({ category: "Item", book: "XDMG", q: "bag of" }),
        ];
        for (const original of combinations) {
            expect(filtersFromQuery(filtersToQuery(original))).toEqual(original);
        }
    });

    it("writes nothing for a filter that is not set, so the plain URL stays plain", () => {
        expect(filtersToQuery(filters())).toEqual({});
        expect(filtersToQuery(filters({ category: "Monster" }))).toEqual({ [CATEGORY_PARAM]: "monster" });
        expect(filtersToQuery(filters({ book: "MM" }))).toEqual({ [BOOK_PARAM]: "MM" });
        expect(filtersToQuery(filters({ q: "  behol  " }))).toEqual({ [QUERY_PARAM]: "behol" });
    });

    it("ignores a category it does not know, rather than filtering to nothing", () => {
        expect(filtersFromQuery({ category: "dragon" }).category).toBeNull();
        expect(filtersFromQuery({}).category).toBeNull();
        expect(filtersFromQuery({ category: ["spell", "item"] }).category).toBe("Spell");
    });

    it("caps what the API would refuse", () => {
        expect(filtersFromQuery({ q: "x".repeat(400) }).q).toHaveLength(QUERY_MAX);
        expect(filtersFromQuery({ book: "b".repeat(400) }).book).toHaveLength(40);
    });

    it("knows whether anything is filtered", () => {
        expect(isFiltered(filters())).toBe(false);
        expect(isFiltered(filters({ q: "   " }))).toBe(false);
        expect(isFiltered(filters({ category: "Monster" }))).toBe(true);
        expect(isFiltered(filters({ book: "MM" }))).toBe(true);
        expect(isFiltered(filters({ q: "behol" }))).toBe(true);
    });

    it("builds a shareable href for the page", () => {
        expect(knowledgeBaseHref("c1")).toBe("/app/campaigns/c1/knowledge-base");
        expect(knowledgeBaseHref("c 1", filters({ category: "Monster", q: "behol" }))).toBe(
            "/app/campaigns/c%201/knowledge-base?category=monster&q=behol"
        );
    });
});

// ── A row links out, and that is the whole interaction ───────────────────────

describe("a row", () => {
    it("links out to 5etools in a new tab, safely", () => {
        expect(rowLink(item())).toEqual({
            href: "https://5e.tools/bestiary.html#beholder_mm",
            target: "_blank",
            rel: "noopener noreferrer",
        });
    });

    it("says where the tap goes", () => {
        expect(rowLinkHint(item())).toBe("Opens on 5eTools in a new tab");
        // A provider that is no longer registered has no label; the key still names it.
        expect(rowLinkHint(item({ providerLabel: null }))).toBe("Opens on 5etools in a new tab");
    });

    it("shows the category and the parser's label, whole — there is no `detail` column", () => {
        expect(rowLine(item())).toBe("Monster · CR 13 · Large Aberration");
        expect(rowLine(item({ label: null }))).toBe("Monster");
        expect(rowLine(item({ category: "Spell", label: "Level 3 Evocation" }))).toBe("Spell · Level 3 Evocation");
    });

    it("shows the book and the page", () => {
        expect(rowSource(item())).toBe("MM · p. 28");
        expect(rowSource(item({ page: null }))).toBe("MM");
    });

    // ── The artwork slot (26g) ───────────────────────────────────────────────

    it("shows the source's artwork by URL, lazily and without a referrer", () => {
        expect(rowArtwork(item({ imageUrl: "https://5e.tools/img/bestiary/MM/Beholder.webp" }))).toEqual({
            src: "https://5e.tools/img/bestiary/MM/Beholder.webp",
            alt: "",
            width: ARTWORK_SIZE,
            height: ARTWORK_SIZE,
            loading: "lazy",
            decoding: "async",
            referrerpolicy: "no-referrer",
        });
    });

    it("falls back to the category glyph when there is no artwork, or it failed", () => {
        // Null, and a URL that 404ed or was blocked: both leave the glyph where it was.
        expect(rowArtwork(item())).toBeNull();
        expect(rowArtwork(item({ imageUrl: null }))).toBeNull();
        expect(rowArtwork(item({ imageUrl: "https://5e.tools/img/gone.webp" }), true)).toBeNull();
        expect(CATEGORY_GLYPHS.Monster).toBe("🐉");
        expect(CATEGORY_GLYPHS.Spell).toBe("🔮");
        expect(CATEGORY_GLYPHS.Item).toBe("🗡️");
    });

    it("counts the corpus above the list", () => {
        expect(countLine(2847, ["5eTools", "5eTools"])).toBe("2,847 items · 5eTools");
        expect(countLine(1, ["5eTools"])).toBe("1 item · 5eTools");
        expect(countLine(0)).toBe("0 items");
    });
});

// ── The two empty states, which are the point of the page ────────────────────

describe("the two empty states", () => {
    it("says nothing has been ingested when the unfiltered corpus is empty", () => {
        expect(knowledgeBaseEmptyState({ total: 0, filters: filters() })).toBe("notIngested");
        expect(KNOWLEDGE_BASE_EMPTY.notIngested).toEqual({
            title: "No reference material yet.",
            detail: "An operator ingests it with the knowledge-base CLI.",
        });
    });

    it("says no items match when there is a corpus and these filters miss it", () => {
        expect(
            knowledgeBaseEmptyState({ total: 0, filters: filters({ category: "Monster" }), corpusEmpty: false })
        ).toBe("noMatch");
        expect(knowledgeBaseEmptyState({ total: 0, filters: filters({ q: "zzz" }), corpusEmpty: false })).toBe(
            "noMatch"
        );
        expect(KNOWLEDGE_BASE_EMPTY.noMatch).toEqual({ title: "No items match.", detail: "Clear filters." });
    });

    it("still says nothing has been ingested when a filtered URL is opened on an empty corpus", () => {
        // A shared `?category=monster` link on a database nobody has ingested into must not
        // blame the filters: that is exactly the confusion this page exists to remove.
        expect(
            knowledgeBaseEmptyState({ total: 0, filters: filters({ category: "Monster" }), corpusEmpty: true })
        ).toBe("notIngested");
    });

    it("says neither while the corpus question is still in flight", () => {
        expect(knowledgeBaseEmptyState({ total: 0, filters: filters({ book: "MM" }) })).toBeNull();
    });

    it("is not an empty state at all when there are rows", () => {
        expect(knowledgeBaseEmptyState({ total: 14, filters: filters() })).toBeNull();
        expect(knowledgeBaseEmptyState({ total: 14, filters: filters({ q: "behol" }), corpusEmpty: false })).toBeNull();
    });
});

// ── The filter values and their facet counts ─────────────────────────────────

describe("the filter values", () => {
    const categories: KnowledgeBaseCategoryFacet[] = [
        { category: "Monster", count: 2103 },
        { category: "Item", count: 126 },
    ];
    const books: KnowledgeBaseBookFacet[] = [
        { book: "MM", bookTitle: "Monster Manual (2014)", count: 400 },
        { book: "XGE", bookTitle: null, count: 12 },
    ];

    it("keeps the three category chips in the corpus's order, with what choosing each would show", () => {
        expect(categoryOptions(categories).map((c) => [c.value, c.count])).toEqual([
            ["Monster", 2103],
            ["Spell", 0],
            ["Item", 126],
        ]);
    });

    it("lists the books the facets name", () => {
        expect(bookOptions(books, null).map((b) => b.value)).toEqual(["MM", "XGE"]);
    });

    it("keeps a chosen book the facets do not name, so the filter can still be unset", () => {
        expect(bookOptions(books, "TCE")[0]).toEqual({ value: "TCE", title: null, count: 0 });
        expect(bookOptions(books, "MM").map((b) => b.value)).toEqual(["MM", "XGE"]);
    });
});

// ── Load more, not infinite scroll ───────────────────────────────────────────

describe("paging", () => {
    it("asks for the rows after the ones on screen, and stops at the total", () => {
        expect(nextSkip(30, 2847)).toBe(30);
        expect(nextSkip(60, 2847)).toBe(60);
        expect(nextSkip(2847, 2847)).toBeUndefined();
        expect(nextSkip(0, 0)).toBeUndefined();
    });
});
