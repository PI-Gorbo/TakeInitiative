<template>
    <main class="flex flex-col gap-4">
        <MeUsernameForm/>
        <section class="flex flex-col gap-2">
            <h2 class="px-1 text-sm font-medium text-muted-foreground">This device</h2>
            <MeSuggestionsSetting/>
        </section>
    </main>
</template>
<script setup lang="ts">
    definePageMeta({
        requiresAuth: true,
        layout: "app",
    });
</script>
