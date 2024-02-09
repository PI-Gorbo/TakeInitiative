<template>
    <div>This is the root page</div>
    <NuxtLink to="/login"> A Link to the login page</NuxtLink>
</template>

<script setup lang="ts">
definePageMeta({
    requiresAuth: true,
});
</script>
