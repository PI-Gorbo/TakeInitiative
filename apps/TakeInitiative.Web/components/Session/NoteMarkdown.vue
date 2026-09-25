<template>
    <!-- The only v-html in the app, and only with renderNoteMarkdown's output
         (raw HTML escaped, unsafe links dropped): see utils/markdown.ts. -->
    <!-- eslint-disable-next-line vue/no-v-html -->
    <div
        class="note-markdown break-words"
        v-html="html" />
</template>

<script setup lang="ts">
    import { renderNoteMarkdown } from "~/utils/markdown";

    const props = defineProps<{ text: string }>();
    const html = computed(() => renderNoteMarkdown(props.text));
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
