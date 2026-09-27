<template>
    <!-- The only v-html in the app, and only with renderNoteMarkdown's output
         (raw HTML escaped, unsafe links dropped): see utils/markdown.ts. -->
    <!-- eslint-disable-next-line vue/no-v-html -->
    <div
        class="note-markdown break-words"
        @click="onClick"
        v-html="html" />
</template>

<script setup lang="ts">
    import { resolveEntry } from "~/utils/entries";
    import { renderNoteMarkdown } from "~/utils/markdown";
    import { useEntryDirectory } from "~/utils/queries/entries";

    const props = defineProps<{
        campaignId: string;
        text: string;
        /** An article block (15f): a document, so headings stay headings. */
        document?: boolean;
    }>();

    // Mention chips (15c): an id in the viewer's entry directory is a chip linking to
    // the entry; any other id is its plain text.
    const directory = useEntryDirectory(() => props.campaignId);
    const html = computed(() =>
        renderNoteMarkdown(props.text, {
            campaignId: props.campaignId,
            resolve: (entryId) => resolveEntry(directory.value, entryId),
            document: props.document,
        })
    );

    // A chip is an app link: follow it client-side. A modified click keeps the
    // browser's own behaviour (a new tab or window).
    function onClick(event: MouseEvent) {
        if (event.defaultPrevented || event.button !== 0) return;
        if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
        const chip = (event.target as Element | null)?.closest?.("a.mention");
        const href = chip?.getAttribute("href");
        if (!href) return;
        event.preventDefault();
        void navigateTo(href);
    }
</script>

<style scoped>
    .note-markdown :deep(p),
    .note-markdown :deep(ul),
    .note-markdown :deep(ol),
    .note-markdown :deep(blockquote),
    .note-markdown :deep(pre),
    .note-markdown :deep(table) {
        margin: 0.25rem 0;
    }
    .note-markdown > :deep(:first-child) {
        margin-top: 0;
    }
    .note-markdown > :deep(:last-child) {
        margin-bottom: 0;
    }
    .note-markdown :deep(ul) {
        list-style: disc;
        padding-left: 1.25rem;
    }
    .note-markdown :deep(ol) {
        list-style: decimal;
        padding-left: 1.25rem;
    }
    .note-markdown :deep(a) {
        color: hsl(var(--primary));
        text-decoration: underline;
        text-underline-offset: 2px;
    }
    /* The mention chip: its text, linking to the entry (glossary). */
    .note-markdown :deep(a.mention) {
        display: inline-flex;
        align-items: baseline;
        gap: 0.2em;
        border-radius: 0.375rem;
        background: hsl(var(--gold) / 0.12);
        padding: 0 0.35em;
        color: hsl(var(--gold));
        font-weight: 500;
        text-decoration: none;
        white-space: normal;
    }
    .note-markdown :deep(a.mention:hover),
    .note-markdown :deep(a.mention:focus-visible) {
        background: hsl(var(--gold) / 0.22);
        text-decoration: underline;
    }
    .note-markdown :deep(.mention-icon) {
        font-size: 0.85em;
    }
    .note-markdown :deep(h1),
    .note-markdown :deep(h2),
    .note-markdown :deep(h3),
    .note-markdown :deep(h4),
    .note-markdown :deep(h5),
    .note-markdown :deep(h6) {
        margin: 0.75rem 0 0.25rem;
        font-weight: 600;
        line-height: 1.3;
    }
    .note-markdown :deep(h1) {
        font-size: 1.25rem;
    }
    .note-markdown :deep(h2) {
        font-size: 1.125rem;
    }
    .note-markdown :deep(h3) {
        font-size: 1rem;
    }
    .note-markdown :deep(blockquote) {
        border-left: 3px solid hsl(var(--border));
        padding-left: 0.75rem;
        color: hsl(var(--muted-foreground));
    }
    .note-markdown :deep(code) {
        border-radius: 0.25rem;
        background: hsl(var(--muted));
        padding: 0.1rem 0.3rem;
        font-size: 0.875em;
    }
    .note-markdown :deep(pre) {
        overflow-x: auto;
        border-radius: 0.375rem;
        background: hsl(var(--muted));
        padding: 0.5rem 0.75rem;
    }
    .note-markdown :deep(pre code) {
        background: none;
        padding: 0;
    }
    .note-markdown :deep(hr) {
        margin: 0.5rem 0;
        border-color: hsl(var(--border));
    }
    .note-markdown :deep(table) {
        display: block;
        overflow-x: auto;
    }
    .note-markdown :deep(th),
    .note-markdown :deep(td) {
        border: 1px solid hsl(var(--border));
        padding: 0.125rem 0.5rem;
    }
</style>
