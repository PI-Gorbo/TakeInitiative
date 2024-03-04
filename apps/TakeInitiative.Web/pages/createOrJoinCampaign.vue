<template>
    <main>
        <h1 class="m-2 flex justify-center">
            Looks like you aren't apart of, or own any campaigns yet!
        </h1>
        <Tabs notSelectedTabColour="take-navy-medium">
            <template #Create>
                <CreateCampaignForm :submit="createCampaign" />
            </template>
            <template #Join>
                <JoinCampaignForm :submit="joinCampaign" />
            </template>
        </Tabs>
    </main>
</template>

<script setup lang="ts">
import type { CreateCampaignRequest } from "~/utils/api/campaign/createCampaignRequest";
import type { JoinCampaignRequest } from "~/utils/api/campaign/joinCampaignRequest";

definePageMeta({ requiresAuth: true, layout: "auth" });

async function createCampaign(req: CreateCampaignRequest) {
    return await useUserStore()
        .createCampaign(req)
        .then(async () => {
            await navigateTo("/");
        });
}

async function joinCampaign(req: JoinCampaignRequest) {
    return await useUserStore()
        .joinCampaign(req)
        .then(async () => {
            await navigateTo("/");
        });
}
</script>
