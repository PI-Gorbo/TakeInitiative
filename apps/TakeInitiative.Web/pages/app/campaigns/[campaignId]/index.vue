<template>
    <!-- The Campaign tab is the session stream (design §3). -->
    <div
        v-if="campaign"
        class="flex h-full w-full flex-col">
        <!-- 14e puts the filter chips in this row. -->
        <div class="flex shrink-0 items-center justify-end gap-2 border-b px-2">
            <button
                type="button"
                class="flex h-11 min-w-11 items-center justify-center gap-2 rounded-md px-2 text-sm text-muted-foreground hover:bg-accent hover:text-accent-foreground"
                :aria-label="`Members (${campaign.members.length})`"
                @click="membersOpen = true">
                <Users class="size-5" />
                <span>{{ campaign.members.length }}</span>
                <span class="hidden sm:inline">Members</span>
            </button>
        </div>

        <div class="mx-auto flex min-h-0 w-full max-w-3xl flex-1 flex-col">
            <SessionStream
                ref="stream"
                :campaignId="campaign.id"
                :campaign="campaign"
                :focusNoteId="focusNoteId"
                @noteOpened="clearNoteLink" />
            <Composer
                :key="campaign.id"
                :campaign="campaign" />
        </div>

        <CampaignMembersPanel
            v-model:open="membersOpen"
            :campaign="campaign" />
    </div>
</template>

<script setup lang="ts">
    import { useQuery } from "@tanstack/vue-query";
    import { Users } from "lucide-vue-next";
    import { NOTE_LINK_PARAM } from "~/utils/noteActions";
    import { getCampaignQuery } from "~/utils/queries/campaign";

    definePageMeta({
        layout: "campaign",
        requiresAuth: true,
    });

    const route = useRoute("app-campaigns-campaignId");
    const campaignQuery = useQuery(getCampaignQuery(() => route.params.campaignId as string));
    const campaign = computed(() => campaignQuery.data.value);

    const membersOpen = ref(false);

    // A copied note link: `?note={noteId}`. The stream opens at the note, then the
    // parameter is dropped so a reload opens at the bottom again.
    const router = useRouter();
    const focusNoteId = computed(() => {
        const value = route.query[NOTE_LINK_PARAM];
        return typeof value === "string" && value ? value : undefined;
    });
    function clearNoteLink() {
        const { [NOTE_LINK_PARAM]: _, ...query } = route.query;
        void router.replace({ query });
    }
</script>
