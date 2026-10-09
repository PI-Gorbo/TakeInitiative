<template>
    <main class="flex flex-col gap-4">
        <MeUsernameForm/>
        <section class="flex flex-col gap-2">
            <h2 class="px-1 text-sm font-medium text-muted-foreground">This device</h2>
            <MeSuggestionsSetting/>
        </section>
        <!-- Not about this device: what the viewer accepted, on every device (23e). -->
        <section class="flex flex-col gap-2">
            <h2 class="px-1 text-sm font-medium text-muted-foreground">Your notes</h2>
            <SuggestionsAcceptedSuggestions/>
        </section>
    </main>
</template>
<script setup lang="ts">
    definePageMeta({
        requiresAuth: true,
        layout: "app",
    });
</script>
