<template>
    <main>Take Initiative Landing Page</main>
</template>

<script setup lang="ts">
useHead({
    title: "Take Initiative",
});

definePageMeta({
    requiresAuth: true,
    middleware: ["index-reroute"],
});
</script>
