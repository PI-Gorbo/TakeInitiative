<template>{{ route.params.draftCombatId }}</template>
<script setup lang="ts">
    const route = useRoute("app-campaigns-id-combats-drafts-draftCombatId");
    definePageMeta({
        layout: "campaign-combats",
        requiresAuth: true,
        layoutTransition: false,
    });
</script>
