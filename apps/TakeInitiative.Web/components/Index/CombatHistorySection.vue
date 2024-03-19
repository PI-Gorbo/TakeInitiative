<template>
    <main>
        {{ campaignStore.state }}
        <section v-for="combat in previousCombats">
            {{ combat.name }}
        </section>
    </main>
</template>
<script setup lang="ts">
const campaignStore = useCampaignStore();
const previousCombats = computed(() => campaignStore.state.finishedCombats);
</script>
