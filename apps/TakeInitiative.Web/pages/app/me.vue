<template>
    <main>
        <MeUsernameForm/>
    </main>
</template>
<script setup lang="ts">
    definePageMeta({
        requiresAuth: true,
        layout: "app",
    });
</script>
