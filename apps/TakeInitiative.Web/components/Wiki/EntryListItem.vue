<template>
    <!-- One row of the wiki list (25g): name, kind, "Played by X" for a player character,
         a one-line summary gist (or "No summary yet, N notes to pick from"), and
         "N notes · last in Session X". The gist and counts are the viewer's own. The entry's
         primary image fills the kind-glyph slot when it has one (SAM-12), the way a knowledge
         base row's artwork does: the glyph is the slot's background, so an image that fails to
         load leaves it exactly where it was and the row never moves. -->
    <NuxtLink
        :to="`/app/campaigns/${encodeURIComponent(campaignId)}/wiki/${encodeURIComponent(item.entry.id)}`"
        class="flex min-h-14 items-start gap-3 rounded-md border px-3 py-2.5 transition-colors hover:bg-accent/50 focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring">
        <span
            class="mt-0.5 flex size-9 shrink-0 items-center justify-center overflow-hidden rounded-md bg-muted text-lg"
            aria-hidden="true">
            <img
                v-if="item.entry.primaryImageId && !imageFailed"
                :src="src(campaignId, item.entry.primaryImageId, 'thumb')"
                alt=""
                class="size-9 object-cover"
                loading="lazy"
                decoding="async"
                @error="imageFailed = true" />
            <template v-else>{{ ENTRY_KIND_ICONS[item.entry.kind] }}</template>
        </span>
        <span class="flex min-w-0 flex-1 flex-col gap-0.5">
            <span class="flex min-w-0 items-start gap-2">
                <span class="min-w-0 flex-1 truncate font-medium">{{ item.entry.name }}</span>
                <span
                    v-if="item.entry.visibility !== 'Everyone'"
                    class="shrink-0 rounded border px-1 text-xs text-muted-foreground"
                    :title="item.entry.visibility === 'DM' ? 'Visible to the DMs and the creator' : 'Visible only to the creator'">
                    🔒 {{ item.entry.visibility }}
                </span>
                <span
                    v-if="playedBy"
                    class="max-w-[50%] shrink-0 truncate rounded-full border border-gold/50 px-2 text-xs leading-5 text-gold">
                    {{ playedBy }}
                </span>
            </span>
            <span class="truncate text-xs text-muted-foreground">
                {{ [item.entry.kind, aliasesLabel(item.entry.aliases)].filter(Boolean).join(" · ") }}
            </span>
            <span
                v-if="item.summaryGist"
                class="truncate text-sm">
                {{ item.summaryGist }}
            </span>
            <span
                v-else
                class="truncate text-sm italic text-muted-foreground">
                {{ emptyGistLabel(item.noteCount) }}
            </span>
            <span class="truncate text-xs text-muted-foreground">{{ entryMetaLabel(item) }}</span>
        </span>
    </NuxtLink>
</template>

<script setup lang="ts">
    import type { EntryListItem } from "~/utils/api/types";
    import { ENTRY_KIND_ICONS, aliasesLabel, emptyGistLabel, entryMetaLabel, playedByLabel } from "~/utils/entries";

    const props = defineProps<{
        campaignId: string;
        item: EntryListItem;
        /** The viewer's member id, for "Played by you". */
        viewerMemberId?: string;
        /** A member's name, or undefined when unknown ("Played by a player"). */
        nameOf: (memberId: string) => string | undefined;
    }>();

    const src = useImageUrl();
    const imageFailed = ref(false);
    watch(
        () => props.item.entry.primaryImageId,
        () => (imageFailed.value = false)
    );

    const playedBy = computed(() =>
        props.item.entry.claimedByMemberId
            ? playedByLabel(props.item.entry.claimedByMemberId, props.viewerMemberId, props.nameOf)
            : null
    );
</script>
