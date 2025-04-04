<template>
    <div>{{ route.params.draftCombatId }}</div>
</template>
<script setup lang="ts">
    const route = useRoute("app-campaigns-id-combats-drafts-draftCombatId");
    definePageMeta({
        layout: "campaign-combats",
        requiresAuth: true,
    });
</script>
