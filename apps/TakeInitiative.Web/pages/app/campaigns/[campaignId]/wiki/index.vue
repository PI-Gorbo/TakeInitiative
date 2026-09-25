<template>
    <EmptyState
        :icon="BookOpen"
        title="The Wiki is empty">
        Entries for the characters, places and factions in your session notes
        arrive in step 15.
    </EmptyState>
</template>

<script setup lang="ts">
    import { BookOpen } from "lucide-vue-next";

    definePageMeta({
        layout: "campaign",
        requiresAuth: true,
    });
</script>
