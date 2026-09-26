<template>
    <div
        class="mx-auto flex max-w-sm flex-col items-center gap-3 px-6 py-16 text-center">
        <component
            :is="props.icon"
            class="size-12 text-muted-foreground"
            aria-hidden="true" />
        <h2 class="text-lg font-semibold">{{ props.title }}</h2>
        <p class="text-sm text-muted-foreground">
            <slot />
        </p>
    </div>
</template>

<script setup lang="ts">
    import type { Component } from "vue";

    const props = defineProps<{
        icon: Component;
        title: string;
    }>();
</script>
