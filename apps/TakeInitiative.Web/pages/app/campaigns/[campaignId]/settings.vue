<template>
    <!-- Campaign settings (SAM-22). Stacked sections, so later settings are an append. -->
    <div class="mx-auto flex w-full max-w-2xl flex-col gap-4 p-4">
        <h1 class="px-1 font-NovaCut text-2xl text-gold">Campaign settings</h1>
        <CampaignNameForm
            v-if="campaign"
            :campaign="campaign" />
    </div>
</template>

<script setup lang="ts">
    import { useQuery } from "@tanstack/vue-query";
    import { canManageCampaign } from "~/utils/campaign";
    import { getCampaignQuery } from "~/utils/queries/campaign";

    definePageMeta({
        requiresAuth: true,
        layout: "campaign",
    });

    const route = useRoute();
    const campaignId = computed(() => (route.params as { campaignId?: string }).campaignId ?? "");

    const campaignQuery = useQuery(getCampaignQuery(campaignId));
    const campaign = computed(() => campaignQuery.data.value ?? null);

    // Settings are a DM's. A Player who lands here goes back to the campaign tab.
    watch(
        [campaign, campaignQuery.isLoading],
        ([loaded, isLoading]) => {
            if (isLoading || !loaded || canManageCampaign(loaded)) return;
            void navigateTo({
                name: "app-campaigns-campaignId",
                params: { campaignId: campaignId.value },
            });
        },
        { immediate: true }
    );
</script>
