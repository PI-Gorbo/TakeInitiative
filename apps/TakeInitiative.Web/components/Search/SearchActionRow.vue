<template>
    <!-- One ⌘K action (17c): "▶ Start Session 14", "→ Go to Wiki", "✎ Post a note about …". -->
    <div class="flex min-w-0 flex-1 items-center gap-3">
        <span
            class="flex size-9 shrink-0 items-center justify-center rounded-md bg-muted text-base"
            aria-hidden="true">
            <LoaderCircle
                v-if="pending"
                class="size-4 animate-spin" />
            <template v-else>{{ icon }}</template>
        </span>
        <span class="min-w-0 flex-1 truncate">{{ label }}</span>
    </div>
</template>

<script setup lang="ts">
    import { LoaderCircle } from "lucide-vue-next";

    defineProps<{ icon: string; label: string; pending?: boolean }>();
</script>
