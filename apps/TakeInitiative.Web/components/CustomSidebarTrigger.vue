<template>
    <button @click="toggleSidebar">
        <slot :open="open" />
    </button>
</template>
<script setup lang="ts">
    import { useSidebar } from "~/components/ui/sidebar";
    const { toggleSidebar, open } = useSidebar();
</script>
