<template>
    <!-- An entry's source (20d): "📖 From SRD 5.2 · Goblin Warrior [view]". The API sends
         `source` only to viewers who may read it (20b), so a player never sees an NPC's.
         [view] opens our card, or the item's page for a search-only provider (21); an item
         that has gone from the data shows its stored id and no link. -->
    <p class="flex min-w-0 flex-wrap items-center gap-x-1.5 text-sm text-muted-foreground">
        <span aria-hidden="true">📖</span>
        <span>From {{ source.providerLabel }}</span>
        <span aria-hidden="true">·</span>
        <span class="min-w-0 truncate font-medium text-foreground">{{ sourceName(source) }}</span>
        <NuxtLink
            v-if="link && 'to' in link"
            :to="link.to"
            class="inline-flex min-h-11 items-center px-1 text-gold underline-offset-2 hover:underline md:min-h-0"
            :aria-label="`View ${sourceName(source)}`">
            [view]
        </NuxtLink>
        <a
            v-else-if="link && 'href' in link"
            :href="link.href"
            target="_blank"
            rel="noopener"
            class="inline-flex min-h-11 items-center gap-1 px-1 text-gold underline-offset-2 hover:underline md:min-h-0"
            :aria-label="`View ${sourceName(source)} (opens in a new tab)`">
            [view]
            <ExternalLink
                class="size-3.5"
                aria-hidden="true" />
        </a>
    </p>
</template>

<script setup lang="ts">
    import { ExternalLink } from "lucide-vue-next";
    import type { EntrySource } from "~/utils/api/types";
    import { sourceLink, sourceName } from "~/utils/reference";

    const props = defineProps<{ campaignId: string; source: EntrySource }>();
    const link = computed(() => sourceLink(props.campaignId, props.source));
</script>
