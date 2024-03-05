<template></template>
<script setup lang="ts">
const route = useRoute();
const combatStore = useCombatStore();
const combatId = route.params.id;

const { refresh, pending, error } = useAsyncData(
    "Campaign",
    () => {
        return campaignStore.init().then(() => true);
    },
    { watch: [() => userStore.state.selectedCampaignId] }
</script>
