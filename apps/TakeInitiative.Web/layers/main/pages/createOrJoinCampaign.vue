<template>
    <main>
        <h1 class="m-2 flex justify-center">
            Create a new campaign or join one with a code
        </h1>
        <Tabs
            backgroundColour="take-purple-dark"
            notSelectedTabColour="take-purple-dark"
        >
            <template #Create>
                <CreateCampaignForm :submit="createCampaign" class="px-2" />
            </template>
            <template #Join>
                <JoinCampaignForm :submit="joinCampaign" class="px-2" />
            </template>
        </Tabs>
    </main>
</template>

<script setup lang="ts">
import type { CreateCampaignRequest } from "base/utils/api/campaign/createCampaignRequest";
import type { JoinCampaignRequest } from "base/utils/api/campaign/joinCampaignRequest";

definePageMeta({ requiresAuth: true, layout: "auth" });
const userStore = useUserStore();
async function createCampaign(req: CreateCampaignRequest) {
    return await userStore
        .createCampaign(req)
        .then(async (c) => await useNavigator().toCampaignTab(c.id, "summary"));
}

async function joinCampaign(req: JoinCampaignRequest) {
    return await useUserStore()
        .joinCampaign(req)
        .then(async (c) => await useNavigator().toCampaignTab(c.id, "summary"));
}
</script>
