<template>
    <!-- One ⌘K hit (17b): an entry, a note, an image note, a session, a combat (18f) or a
         reference item (20c). -->
    <div class="flex min-w-0 flex-1 items-start gap-3">
        <template v-if="hit.entry">
            <!-- The entry's primary image where its kind glyph goes (SAM-12). The glyph is the
                 slot's background, so an image that fails to load leaves it in place. -->
            <span
                class="flex size-9 shrink-0 items-center justify-center overflow-hidden rounded-md bg-muted text-lg"
                aria-hidden="true">
                <img
                    v-if="hit.entry.entry.primaryImageId && !imageFailed"
                    :src="imageSrc(campaignId, hit.entry.entry.primaryImageId, 'thumb')"
                    alt=""
                    class="size-9 object-cover"
                    loading="lazy"
                    decoding="async"
                    @error="imageFailed = true" />
                <template v-else>{{ ENTRY_KIND_ICONS[hit.entry.entry.kind] }}</template>
            </span>
            <span class="flex min-w-0 flex-1 flex-col">
                <span class="flex min-w-0 items-baseline gap-2">
                    <span class="truncate font-medium">{{ hit.entry.entry.name }}</span>
                    <span
                        v-if="hit.entry.alias"
                        class="shrink-0 truncate text-xs text-muted-foreground">
                        aka {{ hit.entry.alias }}
                    </span>
                    <span
                        v-if="lockLabel(hit.entry.entry.visibility)"
                        class="shrink-0 text-xs text-muted-foreground">
                        {{ lockLabel(hit.entry.entry.visibility) }}
                    </span>
                </span>
                <span class="truncate text-xs text-muted-foreground">
                    {{ hit.entry.entry.kind }} · {{ mentionCountLabel(hit.entry.mentionCount) }}
                </span>
                <SearchSnippet
                    v-if="hit.entry.snippet"
                    :snippet="hit.entry.snippet" />
            </span>
        </template>

        <template v-else-if="hit.note">
            <span
                class="flex size-9 shrink-0 items-center justify-center text-muted-foreground"
                aria-hidden="true">
                <ImageIcon
                    v-if="section === 'Images'"
                    class="size-5" />
                <ScrollText
                    v-else-if="hit.note.isRecap"
                    class="size-5" />
                <MessageSquare
                    v-else
                    class="size-5" />
            </span>
            <span class="flex min-w-0 flex-1 flex-col gap-1">
                <span
                    v-if="section === 'Images' && hit.note.images.length > 0"
                    class="flex gap-1">
                    <img
                        v-for="image in hit.note.images.slice(0, 4)"
                        :key="image.id"
                        :src="imageSrc(campaignId, image.id, 'thumb')"
                        alt=""
                        loading="lazy"
                        class="size-12 rounded object-cover" />
                </span>
                <SearchSnippet :snippet="hit.note.snippet" />
                <span class="truncate text-xs text-muted-foreground">
                    {{ noteMeta }}
                </span>
            </span>
        </template>

        <template v-else-if="hit.session">
            <span
                class="flex size-9 shrink-0 items-center justify-center text-muted-foreground"
                aria-hidden="true">
                <CalendarDays class="size-5" />
            </span>
            <span class="flex min-w-0 flex-1 flex-col">
                <!-- The title moves under the line when it is the match, highlighted. -->
                <span class="truncate font-medium">
                    {{
                        sessionLine(
                            hit.session.session.number,
                            formatSessionDate(hit.session.session.startedAt),
                            hit.session.snippet ? null : hit.session.session.title
                        )
                    }}
                </span>
                <SearchSnippet
                    v-if="hit.session.snippet"
                    :snippet="hit.session.snippet" />
            </span>
        </template>

        <template v-else-if="hit.combat">
            <span
                class="flex size-9 shrink-0 items-center justify-center text-muted-foreground"
                aria-hidden="true">
                <Swords class="size-5" />
            </span>
            <span class="flex min-w-0 flex-1 flex-col">
                <span class="truncate font-medium">{{ hit.combat.combat.name }}</span>
                <span
                    :class="[
                        'truncate text-xs',
                        hit.combat.combat.status === 'Active' ? 'text-gold' : 'text-muted-foreground',
                    ]">
                    {{ combatMeta }}
                </span>
            </span>
        </template>

        <template v-else-if="hit.reference">
            <span
                class="flex size-9 shrink-0 items-center justify-center rounded-md bg-muted text-lg"
                aria-hidden="true">
                📖
            </span>
            <span class="flex min-w-0 flex-1 flex-col">
                <span class="flex min-w-0 items-center gap-1.5">
                    <span class="truncate font-medium">{{ hit.reference.name }}</span>
                    <ExternalLink
                        v-if="!hit.reference.hasStatBlock && hit.reference.url"
                        class="size-3.5 shrink-0 text-muted-foreground"
                        aria-label="Opens in a new tab" />
                </span>
                <span class="truncate text-xs text-muted-foreground">
                    {{ referenceHitLine(hit.reference) }}
                </span>
            </span>
        </template>
    </div>
</template>

<script setup lang="ts">
    import { CalendarDays, ExternalLink, ImageIcon, MessageSquare, ScrollText, Swords } from "lucide-vue-next";
    import type { SearchHit, SearchSectionKey } from "~/utils/api/types";
    import { ENTRY_KIND_ICONS, mentionCountLabel } from "~/utils/entries";
    import { referenceHitLine } from "~/utils/reference";
    import { combatHitLine, lockLabel, sessionLine } from "~/utils/search";
    import { formatSessionDate } from "~/utils/sessionDates";

    const props = defineProps<{
        campaignId: string;
        hit: SearchHit;
        section: SearchSectionKey;
        authorName: (memberId: string) => string;
    }>();

    const imageSrc = useImageUrl();
    const imageFailed = ref(false);
    watch(
        () => props.hit.entry?.entry.primaryImageId,
        () => (imageFailed.value = false)
    );

    // "Live · Round 3 · Goblin 2": the line, then the combatant that matched when it
    // was not the combat's own name.
    const combatMeta = computed(() => {
        const hit = props.hit.combat;
        if (!hit) return "";
        return [combatHitLine(hit.combat, hit.sessionNumber), hit.matchedCombatant].filter(Boolean).join(" · ");
    });

    // "Sam · S12 · Sat 20 Sep · 📜 Recap · 🔒 DM"; an image note leaves the date out.
    const noteMeta = computed(() => {
        const note = props.hit.note;
        if (!note) return "";
        return [
            props.authorName(note.authorMemberId),
            `S${note.sessionNumber}`,
            props.section === "Images" ? null : formatSessionDate(note.postedAt),
            note.isRecap ? "📜 Recap" : null,
            lockLabel(note.visibility),
        ]
            .filter(Boolean)
            .join(" · ");
    });
</script>
