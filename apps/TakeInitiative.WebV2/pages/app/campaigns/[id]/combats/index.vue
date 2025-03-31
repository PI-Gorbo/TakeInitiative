<template>
    <div>
        {{ campaignCombatStore.selectedPlannedCombat }}
    </div>
</template>
<script setup lang="ts">
    definePageMeta({
        layout: "campaign-combats",
        requiresAuth: true,
        layoutTransition: false,
    });


    const campaignCombatStore = useCampaignCombatsStore()
</script>
