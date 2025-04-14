<template>Combat Id {{ route.params.combatId }}</template>
<script setup lang="ts">
    const route = useRoute("app-campaigns-campaignId-combats-combatId");
    definePageMeta({
        layout: "campaign-combats",
        requiresAuth: true,
        layoutTransition: false,
    });
</script>
