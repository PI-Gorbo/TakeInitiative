<template>
    <!-- One link on an entry (27d): "↗ Beholder" over its muted line, with `⋯` for Copy link
         and Remove. The two kinds draw the same row, because the point of the abstraction is
         that a third kind is a new case and nothing else.

         A **stale** knowledge-base link (its row has gone from the corpus, or a prune marked
         it) is muted, says "no longer in your knowledge base" in place of its detail, and has
         **no ↗ and no href**: its `url` comes back null, so there is nowhere honest to send the
         reader. It is still removable — the ingest does not get to erase a member's link.

         A label is plain text, never markdown: rendering it would make it an injection
         surface for no benefit. -->
    <li
        class="flex items-start gap-1"
        :class="{ 'opacity-60': pending }">
        <component
            :is="anchor ? 'a' : 'span'"
            v-bind="anchor ?? {}"
            class="-mx-2 flex min-h-11 min-w-0 flex-1 flex-col justify-center rounded-md px-2 py-1 md:min-h-9"
            :class="
                anchor
                    ? 'hover:bg-accent hover:text-accent-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring'
                    : 'text-muted-foreground'
            ">
            <span class="flex min-w-0 items-center gap-1.5">
                <ArrowUpRight
                    v-if="anchor"
                    class="size-4 shrink-0 text-muted-foreground"
                    aria-hidden="true" />
                <span class="min-w-0 flex-1 truncate text-sm">{{ title }}</span>
            </span>
            <span
                v-if="detail"
                class="truncate text-xs text-muted-foreground"
                :title="link.bookTitle ?? undefined">
                {{ detail }}
            </span>
            <span
                v-if="anchor"
                class="sr-only">
                {{ hint }}
            </span>
        </component>

        <DropdownMenu v-if="canWrite && !pending">
            <DropdownMenuTrigger
                :aria-label="`More actions for ${title}`"
                class="flex size-11 shrink-0 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-accent-foreground md:size-9">
                <MoreHorizontal
                    class="size-4"
                    aria-hidden="true" />
            </DropdownMenuTrigger>
            <DropdownMenuContent
                align="end"
                class="w-44">
                <!-- Copy link writes the clipboard inside the tap: iOS refuses a write that
                     happens after the gesture has finished (14e's note sheet, same reason). A
                     stale link has no url, so it is not offered. -->
                <DropdownMenuItem
                    v-if="anchor"
                    class="min-h-11 gap-2 md:min-h-8"
                    @select="copy">
                    <Link
                        class="size-4"
                        aria-hidden="true" />
                    Copy link
                </DropdownMenuItem>
                <DropdownMenuItem
                    class="min-h-11 gap-2 text-destructive-tint md:min-h-8"
                    @select="emit('remove')">
                    <Trash2
                        class="size-4"
                        aria-hidden="true" />
                    Remove
                </DropdownMenuItem>
            </DropdownMenuContent>
        </DropdownMenu>
    </li>
</template>

<script setup lang="ts">
    import { ArrowUpRight, Link, MoreHorizontal, Trash2 } from "lucide-vue-next";
    import { toast } from "vue-sonner";
    import type { EntryLink } from "~/utils/api/types";
    import { isPendingLink, linkAnchor, linkDetail, linkHint, linkTitle } from "~/utils/links";

    const props = defineProps<{
        link: EntryLink;
        /** Whether the viewer may remove it (`canWriteLinks`). */
        canWrite: boolean;
    }>();
    const emit = defineEmits<{ remove: [] }>();

    const title = computed(() => linkTitle(props.link));
    const detail = computed(() => linkDetail(props.link));
    const hint = computed(() => linkHint(props.link));
    const anchor = computed(() => linkAnchor(props.link));
    /** An optimistic row: it has no server id yet, so there is nothing to remove or copy. */
    const pending = computed(() => isPendingLink(props.link));

    async function copy() {
        const href = anchor.value?.href;
        if (!href) return;
        try {
            await navigator.clipboard.writeText(href);
            toast.success("Link copied.");
        } catch {
            toast.error("Could not copy the link.");
        }
    }
</script>
