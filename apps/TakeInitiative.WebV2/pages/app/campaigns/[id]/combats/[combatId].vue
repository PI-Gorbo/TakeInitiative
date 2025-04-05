<template>Combat Id {{ route.params.combatId }}</template>
<script setup lang="ts">
    const route = useRoute("app-campaigns-id-combats-combatId");
    definePageMeta({
        layout: "campaign-combats",
        requiresAuth: true,
    });
</script>
