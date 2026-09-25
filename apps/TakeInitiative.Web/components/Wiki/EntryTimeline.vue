<template>
    <!-- An entry's timeline (glossary, design §4, 15c): every session note the viewer
         can see that mentions it, oldest first, paged with "Load older". Read-only:
         editing a note is the only way to change it. -->
    <section
        :aria-labelledby="`${id}-title`"
        class="flex flex-col gap-1">
        <h3
            :id="`${id}-title`"
            class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            Timeline
        </h3>
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
                    Load older
                </Button>
            </div>
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
                        :isDm="isDm">
                        <!-- Design §4: [Promote] on each timeline note, into this entry. -->
                        <template #actions="{ actions, run }">
                            <Button
                                v-if="actions.includes('promote')"
                                variant="ghost"
                                size="sm"
                                class="-my-2 ml-auto h-11 gap-1 text-xs text-muted-foreground md:-my-1 md:h-7"
                                :aria-label="`Promote this note to ${entryName}'s article`"
                                @click="run('promote')">
                                <BookPlus
                                    class="size-3.5"
                                    aria-hidden="true" />
                                Promote
                            </Button>
                        </template>
                    </SessionNoteCard>
                </li>
            </ol>
        </template>
        <!-- Articles that mention this entry (15e). Only blocks the viewer can see count. -->
        <p
            v-if="articleMentions.length > 0"
            class="flex flex-wrap items-center gap-x-2 gap-y-1 pt-1 text-sm text-muted-foreground">
            <span>Also mentioned in the articles of</span>
            <NuxtLink
                v-for="mention in articleMentions"
                :key="mention.id"
                :to="entryHref(campaign.id, mention.id)"
                class="inline-flex min-h-11 items-center gap-1 rounded-md px-1 font-medium text-gold hover:underline md:min-h-0">
                <span aria-hidden="true">{{ ENTRY_KIND_ICONS[mention.kind] }}</span>
                {{ mention.name }}
            </NuxtLink>
        </p>

        <!-- Desktop: "Add to wiki" by a selection inside a timeline note (15f). -->
        <WikiPromoteSelection
            :campaignId="campaign.id"
            :viewer="{ memberId: campaign.currentMemberId, isDm }"
            :findNote="findNote"
            :entryId="canEdit ? entryId : undefined" />
    </section>
</template>

<script setup lang="ts">
    import { useInfiniteQuery } from "@tanstack/vue-query";
    import { BookPlus, LoaderCircle } from "lucide-vue-next";
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
        /** The viewer can edit this entry: each note offers Promote into it (15f). */
        canEdit: boolean;
    }>();

    const id = useId();
    const timelineQuery = useInfiniteQuery(
        getEntryTimelineQuery(
            () => props.campaign.id,
            () => props.entryId
        )
    );
    const items = computed(() => timelineItems(timelineQuery.data.value));
    const findNote = (noteId: string) => items.value.find((i) => i.note.id === noteId)?.note;

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
