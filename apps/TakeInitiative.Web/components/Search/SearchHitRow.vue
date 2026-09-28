<template>
    <!-- One ⌘K hit (17b): an entry, a note, an image note or a session. -->
    <div class="flex min-w-0 flex-1 items-start gap-3">
        <template v-if="hit.entry">
            <span
                class="flex size-9 shrink-0 items-center justify-center rounded-md bg-muted text-lg"
                aria-hidden="true">
                {{ ENTRY_KIND_ICONS[hit.entry.entry.kind] }}
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
    </div>
</template>

<script setup lang="ts">
    import { CalendarDays, ImageIcon, MessageSquare, ScrollText } from "lucide-vue-next";
    import type { SearchHit, SearchSectionKey } from "~/utils/api/types";
    import { ENTRY_KIND_ICONS, mentionCountLabel } from "~/utils/entries";
    import { lockLabel, sessionLine } from "~/utils/search";
    import { formatSessionDate } from "~/utils/sessionDates";

    const props = defineProps<{
        campaignId: string;
        hit: SearchHit;
        section: SearchSectionKey;
        authorName: (memberId: string) => string;
    }>();

    const imageSrc = useImageUrl();

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
