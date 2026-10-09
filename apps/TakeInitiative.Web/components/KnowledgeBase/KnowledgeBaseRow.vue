<template>
    <!-- One knowledge base row (26f). A tap opens 5etools in a new tab, and that is the
         whole interaction: there is no in-app detail page for a 5eTools item, because the
         corpus holds nothing we are allowed to show. Name, category, the parser's `label`,
         the book, the page and (26g) the artwork by URL — the whole allowlist. -->
    <li>
        <a
            v-bind="rowLink(item)"
            class="flex items-start gap-3 rounded-lg border bg-card p-3 transition-colors hover:border-gold/40 hover:bg-accent focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring">
            <!-- The artwork slot (26g). The picture is 5eTools', loaded by the browser from
                 their CDN: nothing is fetched or stored on our side, and the URL is all the
                 corpus holds. `no-referrer` keeps our URL, which carries a campaign id, out of
                 their logs; `lazy` keeps a thousand rows from being a thousand requests; and the
                 size is fixed on the slot, not on the image, so the row never moves as pictures
                 arrive. An image that is null, fails, or is blocked leaves the category glyph
                 exactly where it was — the glyph is the slot's background, not a replacement. -->
            <span
                class="flex size-11 shrink-0 items-center justify-center overflow-hidden rounded-md bg-muted text-xl"
                aria-hidden="true">
                <img
                    v-if="artwork"
                    v-bind="artwork"
                    class="size-11 object-cover"
                    @error="imageFailed = true" />
                <template v-else>{{ glyph }}</template>
            </span>
            <span class="min-w-0 flex-1">
                <span class="flex items-start gap-2">
                    <span class="min-w-0 flex-1 truncate font-medium">{{ item.name }}</span>
                    <ArrowUpRight
                        class="mt-0.5 size-4 shrink-0 text-muted-foreground"
                        aria-hidden="true" />
                </span>
                <span class="block truncate text-sm text-muted-foreground">{{ line }}</span>
                <span
                    class="block truncate text-xs text-muted-foreground"
                    :title="item.bookTitle ?? undefined">
                    {{ source }}
                </span>
                <span class="sr-only">{{ hint }}</span>
            </span>
        </a>
    </li>
</template>

<script setup lang="ts">
    import { ArrowUpRight } from "lucide-vue-next";
    import type { KnowledgeBaseItem } from "~/utils/api/types";
    import { CATEGORY_GLYPHS, rowArtwork, rowLine, rowLink, rowLinkHint, rowSource } from "~/utils/knowledgeBase";

    const props = defineProps<{ item: KnowledgeBaseItem }>();

    const glyph = computed(() => CATEGORY_GLYPHS[props.item.category]);
    // A URL that 404s, or a blocked request: back to the glyph, per row, until the row changes.
    const imageFailed = ref(false);
    watch(() => props.item.imageUrl, () => (imageFailed.value = false));
    const artwork = computed(() => rowArtwork(props.item, imageFailed.value));
    const line = computed(() => rowLine(props.item));
    const source = computed(() => rowSource(props.item));
    const hint = computed(() => rowLinkHint(props.item));
</script>
