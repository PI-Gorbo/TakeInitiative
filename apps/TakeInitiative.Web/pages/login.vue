<template>
    <div />
</template>

<script setup lang="ts">
    // Signing in (and signing up) is SamStack's now; old links and bookmarks land here.
    definePageMeta({
        requiresAuth: false,
        middleware: (to) => goToSignIn(to.query.redirectTo),
    });
</script>
