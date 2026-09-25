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
                :campaignId="campaign.id"
                :campaign="campaign" />
            <!-- 14d: the Composer mounts here, below the stream. -->
        </div>

        <CampaignMembersPanel
            v-model:open="membersOpen"
            :campaign="campaign" />
    </div>
</template>

<script setup lang="ts">
    import { useQuery } from "@tanstack/vue-query";
    import { Users } from "lucide-vue-next";
    import { getCampaignQuery } from "~/utils/queries/campaign";

    definePageMeta({
        layout: "campaign",
        requiresAuth: true,
    });

    const route = useRoute("app-campaigns-campaignId");
    const campaignQuery = useQuery(getCampaignQuery(() => route.params.campaignId as string));
    const campaign = computed(() => campaignQuery.data.value);

    const membersOpen = ref(false);
</script>
