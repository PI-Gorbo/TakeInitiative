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
                        :authorName="authorName(item.note.authorMemberId)"
                        :currentMemberId="campaign.currentMemberId"
                        :isDm="isDm" />
                </li>
            </ol>
        </template>
    </section>
</template>

<script setup lang="ts">
    import { useInfiniteQuery } from "@tanstack/vue-query";
    import { LoaderCircle } from "lucide-vue-next";
    import type { Campaign } from "~/utils/api/types";
    import { currentMember } from "~/utils/campaign";
    import { timelineItems } from "~/utils/entryCache";
    import { getEntryTimelineQuery } from "~/utils/queries/entries";

    const props = defineProps<{ campaign: Campaign; entryId: string; entryName: string }>();

    const id = useId();
    const timelineQuery = useInfiniteQuery(
        getEntryTimelineQuery(
            () => props.campaign.id,
            () => props.entryId
        )
    );
    const items = computed(() => timelineItems(timelineQuery.data.value));

    const isDm = computed(() => currentMember(props.campaign)?.role === "DM");
    const usernames = computed(() => new Map(props.campaign.members.map((m) => [m.memberId, m.username])));
    const authorName = (memberId: string) => usernames.value.get(memberId) ?? "Unknown member";
</script>
