<template>
    <!-- The category and source filters (26f). On a phone they are two chip rows that scroll
         sideways, like the stream's and the wiki's (25); at `lg` the same values become a
         sticky left rail with the facet count beside each one. Each facet drops its own
         filter, so a count is what choosing that value would show, not what this page holds. -->
    <template v-if="layout === 'chips'">
        <div
            role="group"
            aria-label="Filter by category"
            class="flex min-w-0 items-center gap-1 overflow-x-auto py-1 [scrollbar-width:none]">
            <button
                type="button"
                :aria-pressed="category === null"
                :class="chip(category === null)"
                @click="category = null">
                All
            </button>
            <button
                v-for="option in categories"
                :key="option.value"
                type="button"
                :aria-pressed="category === option.value"
                :class="chip(category === option.value)"
                @click="category = option.value">
                <span aria-hidden="true">{{ option.glyph }}</span>
                {{ option.label }}
            </button>
        </div>
        <div
            role="group"
            aria-label="Filter by source book"
            class="flex min-w-0 items-center gap-1 overflow-x-auto py-1 [scrollbar-width:none]">
            <button
                type="button"
                :aria-pressed="book === null"
                :class="chip(book === null)"
                @click="book = null">
                All books
            </button>
            <button
                v-for="option in books"
                :key="option.value"
                type="button"
                :aria-pressed="book === option.value"
                :title="option.title ?? undefined"
                :class="chip(book === option.value)"
                @click="book = option.value">
                {{ option.value }}
            </button>
        </div>
    </template>

    <!-- The desktop rail (`lg+`): sticky under the app header, scrolling on its own when the
         corpus has more books than fit. -->
    <nav
        v-else
        aria-label="Filters"
        class="sticky top-4 flex max-h-[calc(100dvh-6rem)] flex-col gap-5 overflow-y-auto pr-2">
        <section class="flex flex-col gap-0.5">
            <h2 class="px-2 pb-1 text-xs font-semibold uppercase tracking-wide text-muted-foreground">Category</h2>
            <button
                type="button"
                :aria-pressed="category === null"
                :class="railRow(category === null)"
                @click="category = null">
                <span class="min-w-0 flex-1 truncate text-left">All</span>
            </button>
            <button
                v-for="option in categories"
                :key="option.value"
                type="button"
                :aria-pressed="category === option.value"
                :class="railRow(category === option.value)"
                @click="category = option.value">
                <span aria-hidden="true">{{ option.glyph }}</span>
                <span class="min-w-0 flex-1 truncate text-left">{{ option.label }}</span>
                <span class="shrink-0 text-xs tabular-nums text-muted-foreground">{{ option.count }}</span>
            </button>
        </section>
        <section class="flex flex-col gap-0.5">
            <h2 class="px-2 pb-1 text-xs font-semibold uppercase tracking-wide text-muted-foreground">Source</h2>
            <button
                type="button"
                :aria-pressed="book === null"
                :class="railRow(book === null)"
                @click="book = null">
                <span class="min-w-0 flex-1 truncate text-left">All books</span>
            </button>
            <button
                v-for="option in books"
                :key="option.value"
                type="button"
                :aria-pressed="book === option.value"
                :title="option.title ?? undefined"
                :class="railRow(book === option.value)"
                @click="book = option.value">
                <span class="min-w-0 flex-1 truncate text-left">{{ option.value }}</span>
                <span class="shrink-0 text-xs tabular-nums text-muted-foreground">{{ option.count }}</span>
            </button>
        </section>
    </nav>
</template>

<script setup lang="ts">
    import type { KnowledgeBaseBookFacet, KnowledgeBaseCategoryFacet, ReferenceCategory } from "~/utils/api/types";
    import { bookOptions, categoryOptions } from "~/utils/knowledgeBase";

    const props = defineProps<{
        layout: "chips" | "rail";
        categoryFacets: readonly KnowledgeBaseCategoryFacet[];
        bookFacets: readonly KnowledgeBaseBookFacet[];
    }>();
    const category = defineModel<ReferenceCategory | null>("category", { required: true });
    const book = defineModel<string | null>("book", { required: true });

    const categories = computed(() => categoryOptions(props.categoryFacets));
    const books = computed(() => bookOptions(props.bookFacets, book.value));

    // 44px on a phone, tighter where there is a mouse (25's convention).
    const chip = (on: boolean) => [
        "flex h-11 shrink-0 items-center gap-1 rounded-full px-3 text-sm transition-colors md:h-8",
        on ? "bg-gold/15 font-medium text-gold" : "text-muted-foreground hover:bg-accent hover:text-accent-foreground",
    ];
    const railRow = (on: boolean) => [
        "flex h-9 items-center gap-2 rounded-md px-2 text-sm transition-colors",
        on ? "bg-gold/15 font-medium text-gold" : "text-muted-foreground hover:bg-accent hover:text-accent-foreground",
    ];
</script>
