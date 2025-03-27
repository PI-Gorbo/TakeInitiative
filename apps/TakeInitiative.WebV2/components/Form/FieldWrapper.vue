<template>
    <div class="flex flex-col gap-1">
        <label v-if="props.label" :class="{ 'text-destructive': props.error }">
            {{ props.label }}</label
        >
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
