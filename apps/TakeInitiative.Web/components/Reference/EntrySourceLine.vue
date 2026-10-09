<template>
    <!-- An entry's source (20d): "📖 From SRD 5.2 · Goblin Warrior [view]". The API sends
         `source` only to viewers who may read it (20b), so a player never sees an NPC's.
         [view] opens our card. A search-only provider's item (5eTools, 21c) reads
         "↗ From 5eTools · Beholder (MM p. 28)", and its name and book link out in a new tab,
         with the book's full title as the tooltip. An item that has gone from the data shows
         its stored id and no link. -->
    <p class="flex min-w-0 flex-wrap items-center gap-x-1.5 text-sm text-muted-foreground">
        <span aria-hidden="true">{{ parts.icon }}</span>
        <span>{{ parts.from }}</span>
        <span aria-hidden="true">·</span>
        <a
            v-if="link && 'href' in link"
            :href="link.href"
            target="_blank"
            rel="noopener noreferrer"
            :title="parts.bookTitle ?? undefined"
            class="inline-flex min-h-11 min-w-0 items-center gap-1 text-foreground underline-offset-2 hover:underline md:min-h-0"
            :aria-label="linkLabel">
            <span class="min-w-0 truncate font-medium">{{ parts.name }}</span>
            <span
                v-if="parts.book"
                class="shrink-0 text-muted-foreground">
                {{ parts.book }}
            </span>
            <ExternalLink
                class="size-3.5 shrink-0 text-gold"
                aria-hidden="true" />
        </a>
        <template v-else>
            <span class="min-w-0 truncate font-medium text-foreground">{{ parts.name }}</span>
            <span
                v-if="parts.book"
                :title="parts.bookTitle ?? undefined">
                {{ parts.book }}
            </span>
            <NuxtLink
                v-if="link && 'to' in link"
                :to="link.to"
                class="inline-flex min-h-11 items-center px-1 text-gold underline-offset-2 hover:underline md:min-h-0"
                :aria-label="`View ${parts.name}`">
                [view]
            </NuxtLink>
        </template>
    </p>
</template>

<script setup lang="ts">
    import { ExternalLink } from "lucide-vue-next";
    import type { EntrySource } from "~/utils/api/types";
    import { sourceLineParts, sourceLink } from "~/utils/reference";

    const props = defineProps<{ campaignId: string; source: EntrySource }>();
    const link = computed(() => sourceLink(props.campaignId, props.source));
    const parts = computed(() => sourceLineParts(props.source));
    // "Beholder, MM p. 28, Monster Manual (2014), on 5eTools (opens in a new tab)".
    const linkLabel = computed(() =>
        [parts.value.name, props.source.detail, parts.value.bookTitle, `on ${props.source.providerLabel} (opens in a new tab)`]
            .filter(Boolean)
            .join(", ")
    );
</script>
