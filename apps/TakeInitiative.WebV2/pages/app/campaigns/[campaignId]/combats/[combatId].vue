<template>
    <div class="h-full w-full flex justify-center">
        <div class="lg:grid lg:grid-cols-[min-content_1fr] w-page">
            <div class="border">A</div>
            <div class="border">B</div>
        </div>
    </div>
</template>
<script setup lang="ts">
    const route = useRoute("app-campaigns-campaignId-combats-combatId");
    definePageMeta({
        layout: "main-app",
        requiresAuth: true,
        layoutTransition: false,
    });
</script>
