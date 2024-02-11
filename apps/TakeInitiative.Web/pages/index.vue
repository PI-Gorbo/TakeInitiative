<template>
    <div>
        <div v-if="pending">LOADING...</div>
        <div v-else-if="error">THERE WAS AN ERROR {{ error }}</div>
        <div v-else>
            <div>This is the root page</div>
            <NuxtLink to="/login"> A Link to the login page</NuxtLink>
        </div>
    </div>
</template>

<script setup lang="ts">
import redirectToCreateOrJoinCampaign from "~/middleware/redirectToCreateOrJoinCampaign";
const campaignStore = useCampaignStore();
const { refresh, pending, error } = useAsyncData("", () =>
    campaignStore.init().then(() => true)
); // Return something so that nuxt does not recall this on this client

definePageMeta({
    requiresAuth: true,
    middleware: [redirectToCreateOrJoinCampaign],
});
</script>
