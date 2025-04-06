<template>
    <div class="flex flex-col gap-1">
        <header class="flex justify-between gap-2 text-sm">
            <label
                v-if="props.label"
                :class="{ 'text-destructive': props.error }">
                {{ props.label }}
            </label>
            <slot name="Header" />
        </header>
        <div>
            <slot />
        </div>
        <label v-if="props.error" class="text-destructive">{{
            props.error
        }}</label>
    </div>
</template>
<script setup lang="ts">
    const props = defineProps<{
        label?: string;
        error?: string;
    }>();
</script>
