<template>
    <div>This is the root page</div>
    <NuxtLink to="/login"> A Link to the login page</NuxtLink>
</template>

<script setup lang="ts">
import redirectToCreateOrJoinCampaign from "~/middleware/redirectToCreateOrJoinCampaign";

definePageMeta({
    requiresAuth: true,
    middleware: [redirectToCreateOrJoinCampaign],
});
</script>
