<template>
    <EmptyState
        :icon="Swords"
        title="No combat yet">
        Combat, with initiative, rounds and turns, arrives in step 18.
    </EmptyState>
</template>

<script setup lang="ts">
    import { Swords } from "lucide-vue-next";

    definePageMeta({
        layout: "campaign",
        requiresAuth: true,
    });
</script>
