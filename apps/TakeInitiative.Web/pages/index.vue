<template>
    <div>
        <div v-if="pending">LOADING...</div>
        <div v-else-if="error">{{ JSON.stringify(error.stack, null, "\t") }}</div>
        <div v-else>
            <div>This is the root page</div>
            <NuxtLink to="/login"> A Link to the login page</NuxtLink>
            <div class="select-none cursor-pointer" @click="userStore.signOut">
                A link to sign out
            </div>
        </div>
    </div>
</template>

<script setup lang="ts">
import redirectToCreateOrJoinCampaign from "~/middleware/redirectToCreateOrJoinCampaign";
const userStore = useUserStore();
const { refresh, pending, error } = useAsyncData("Campaign", () => {
    console.log(process.server);
    const campaignStore = useCampaignStore();
    return campaignStore.init().then(() => true);
}); // Return something so that nuxt does not recall this on this client

definePageMeta({
    requiresAuth: true,
    middleware: [redirectToCreateOrJoinCampaign],
});
</script>
