<template>
    <!-- One row of the wiki list: kind, name, aliases and the viewer's mention count. A
         player character (15g) is marked, in the glossary's words. -->
    <NuxtLink
        :to="`/app/campaigns/${encodeURIComponent(campaignId)}/wiki/${encodeURIComponent(item.entry.id)}`"
        class="flex min-h-14 items-center gap-3 rounded-md border px-3 py-2 transition-colors hover:bg-accent/50 focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring">
        <span
            class="flex size-9 shrink-0 items-center justify-center rounded-md bg-muted text-lg"
            aria-hidden="true">
            {{ ENTRY_KIND_ICONS[item.entry.kind] }}
        </span>
        <span class="flex min-w-0 flex-1 flex-col">
            <span class="flex min-w-0 items-baseline gap-2">
                <span class="truncate font-medium">{{ item.entry.name }}</span>
                <span
                    v-if="item.entry.visibility !== 'Everyone'"
                    class="shrink-0 rounded border px-1 text-xs text-muted-foreground"
                    :title="item.entry.visibility === 'DM' ? 'Visible to the DMs and the creator' : 'Visible only to the creator'">
                    🔒 {{ item.entry.visibility }}
                </span>
                <span
                    v-if="item.entry.claimedByMemberId"
                    class="shrink-0 rounded border border-gold/50 px-1 text-xs text-gold">
                    Player character
                </span>
            </span>
            <span class="truncate text-xs text-muted-foreground">
                {{ [item.entry.kind, aliasesLabel(item.entry.aliases)].filter(Boolean).join(" · ") }}
            </span>
        </span>
        <span class="shrink-0 text-xs text-muted-foreground">{{ mentionCountLabel(item.mentionCount) }}</span>
    </NuxtLink>
</template>

<script setup lang="ts">
    import type { EntryListItem } from "~/utils/api/types";
    import { ENTRY_KIND_ICONS, aliasesLabel, mentionCountLabel } from "~/utils/entries";

    defineProps<{ campaignId: string; item: EntryListItem }>();
</script>
