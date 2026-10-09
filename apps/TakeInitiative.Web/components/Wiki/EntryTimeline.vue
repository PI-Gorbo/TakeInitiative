<template>
    <!-- An entry's timeline (glossary, design §4, 15c), the Notes tab in the UI (25d):
         every session note the viewer can see that mentions it, newest first, paged
         with "Load older notes". Read-only: editing a note is the only way to change
         it. Each note offers "Add to summary" (promote, 15f), or shows "✓ In summary"
         when a quote in the summary already comes from it. -->
    <section
        :aria-label="`Notes about ${entryName}`"
        class="flex flex-col gap-1">
        <LoadingFallback
            v-if="!timelineQuery.data.value"
            :isLoading="timelineQuery.isLoading.value"
            :isError="timelineQuery.isError.value"
            iconSize="2x"
            class="py-6" />
        <p
            v-else-if="items.length === 0"
            class="py-4 text-sm text-muted-foreground">
            No session notes mention {{ entryName }} yet.
        </p>
        <template v-else>
            <p class="pb-1 text-sm text-muted-foreground">
                Every note that mentions {{ entryName }}, newest first.<template v-if="canEdit">
                    Add the useful ones to the summary.</template
                >
            </p>
            <ol
                class="-mx-4 flex flex-col"
                role="feed"
                :aria-busy="timelineQuery.isFetching.value">
                <li
                    v-for="item in items"
                    :key="item.note.id">
                    <SessionNoteCard
                        :campaignId="campaign.id"
                        :note="item.note"
                        :timeline="{ sessionNumber: item.sessionNumber }"
                        :promoteEntryId="canEdit ? entryId : undefined"
                        :authorName="authorName(item.note.authorMemberId)"
                        :currentMemberId="campaign.currentMemberId"
                        :isDm="isDm"
                        :highlighted="!!highlightedNoteId && sameId(item.note.id, highlightedNoteId)"
                        @openImage="imageViewer.open">
                        <!-- Design §4 / 25d: "Add to summary" on each note, into this
                             entry, or "✓ In summary" once a quote comes from it. -->
                        <template #actions="{ actions, run }">
                            <span
                                v-if="inSummary.has(item.note.id.toLowerCase())"
                                class="ml-auto flex items-center gap-1 text-xs font-medium text-gold">
                                <Check
                                    class="size-3.5"
                                    aria-hidden="true" />
                                In summary
                            </span>
                            <Button
                                v-else-if="actions.includes('promote')"
                                variant="ghost"
                                size="sm"
                                class="-my-2 ml-auto h-11 gap-1 text-xs text-muted-foreground md:-my-1 md:h-7"
                                :aria-label="`Add this note to ${entryName}'s summary`"
                                @click="run('promote')">
                                <BookPlus
                                    class="size-3.5"
                                    aria-hidden="true" />
                                Add to summary
                            </Button>
                        </template>
                    </SessionNoteCard>
                </li>
            </ol>
            <div
                v-if="timelineQuery.hasNextPage.value"
                class="flex justify-center">
                <Button
                    variant="ghost"
                    class="h-11 text-xs text-muted-foreground md:h-9"
                    :disabled="timelineQuery.isFetchingNextPage.value"
                    @click="timelineQuery.fetchNextPage()">
                    <LoaderCircle
                        v-if="timelineQuery.isFetchingNextPage.value"
                        class="size-4 animate-spin"
                        aria-hidden="true" />
                    Load older notes
                </Button>
            </div>
        </template>
        <!-- Summaries that mention this entry (15e). Only blocks the viewer can see count. -->
        <p
            v-if="articleMentions.length > 0"
            class="flex flex-wrap items-center gap-x-2 gap-y-1 pt-1 text-sm text-muted-foreground">
            <span>Also mentioned in the summaries of</span>
            <NuxtLink
                v-for="mention in articleMentions"
                :key="mention.id"
                :to="entryHref(campaign.id, mention.id)"
                class="inline-flex min-h-11 items-center gap-1 rounded-md px-1 font-medium text-gold hover:underline md:min-h-0">
                <span aria-hidden="true">{{ ENTRY_KIND_ICONS[mention.kind] }}</span>
                {{ mention.name }}
            </NuxtLink>
        </p>

        <!-- The image viewer, following `?image=` (16c). -->
        <ImageViewer
            :campaignId="campaign.id"
            :items="items"
            :authorName="authorName"
            :ready="!!timelineQuery.data.value && !timelineQuery.isFetching.value" />

        <!-- Desktop: "Add to summary" by a selection inside a note (15f). -->
        <WikiPromoteSelection
            :campaignId="campaign.id"
            :viewer="{ memberId: campaign.currentMemberId, isDm }"
            :findNote="findNote"
            :entryId="canEdit ? entryId : undefined" />
    </section>
</template>

<script setup lang="ts">
    import { useInfiniteQuery } from "@tanstack/vue-query";
    import { BookPlus, Check, LoaderCircle } from "lucide-vue-next";
    import type { Campaign } from "~/utils/api/types";
    import { currentMember } from "~/utils/campaign";
    import { entryHref } from "~/utils/article";
    import { ENTRY_KIND_ICONS, resolveEntry } from "~/utils/entries";
    import { timelineItems } from "~/utils/entryCache";
    import { getEntryTimelineQuery, useEntryDirectory } from "~/utils/queries/entries";

    const props = defineProps<{
        campaign: Campaign;
        entryId: string;
        entryName: string;
        /** The viewer can edit this entry: each note offers "Add to summary" into it (15f). */
        canEdit: boolean;
        /** The notes the summary quotes, lower-cased (`quotedNoteIds`): "✓ In summary". */
        inSummary: ReadonlySet<string>;
        /** A note opened from a quote's source chip: marked for a moment (25d). */
        highlightedNoteId?: string | null;
    }>();

    const timelineQuery = useInfiniteQuery(
        getEntryTimelineQuery(
            () => props.campaign.id,
            () => props.entryId
        )
    );
    // Newest first (25d); the pages hold them oldest first.
    const items = computed(() => timelineItems(timelineQuery.data.value).reverse());
    const sameId = (a: string, b: string) => a.toLowerCase() === b.toLowerCase();
    const findNote = (noteId: string) => items.value.find((i) => i.note.id === noteId)?.note;
    const imageViewer = useImageViewer();

    // Every page carries the same list; the newest is enough.
    const directory = useEntryDirectory(() => props.campaign.id);
    const articleMentions = computed(() =>
        (timelineQuery.data.value?.pages[0]?.articleMentions ?? []).flatMap((m) => {
            const entry = resolveEntry(directory.value, m.entryId);
            return entry ? [entry] : [];
        })
    );

    const isDm = computed(() => currentMember(props.campaign)?.role === "DM");
    const usernames = computed(() => new Map(props.campaign.members.map((m) => [m.memberId, m.username])));
    const authorName = (memberId: string) => usernames.value.get(memberId) ?? "Unknown member";
</script>
