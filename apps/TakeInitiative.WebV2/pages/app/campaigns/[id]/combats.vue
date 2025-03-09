<template></template>
<script setup lang="ts">
    const campignId: string = useRoute().params?.id;
    definePageMeta({
        layout: "app",
        requiresAuth: true,
    });
</script>
