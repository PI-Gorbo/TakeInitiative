<template>
    <LoadingFallback
        :isLoading="true"
        iconSize="3x"
        class="pt-16" />
</template>

<script setup lang="ts">
    // The PWA's start_url. Sends the user to their first campaign, or to
    // create/join when they have none (after the global auth check).
    definePageMeta({
        requiresAuth: true,
        middleware: ["index-reroute"],
    });
</script>
