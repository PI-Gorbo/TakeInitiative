<template>
    <ul>
        <li v-if="userHasNoResources"></li>
        <li v-for="resource in memberResources">
            <!-- {{ resource }} -->
        </li>
    </ul>
</template>
<script setup lang="ts">
const campaignStore = useCampaignStore();
const userStore = useUserStore();
const { memberResources } = storeToRefs(campaignStore);
const userHasNoResources = computed(
    () => (campaignStore.state.userCampaignMember?.resources ?? []).length == 0,
);
</script>
