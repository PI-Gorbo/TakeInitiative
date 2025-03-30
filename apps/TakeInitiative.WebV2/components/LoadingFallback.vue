<template>
    <component :is="props.container">
        <div v-if="props.isLoading">Loading...</div>
        <slot v-else />
    </component>
</template>

<script setup lang="ts">
    const props = withDefaults(
        defineProps<{
            isLoading: boolean;
            container: string | Comment;
        }>(),
        {
            container: "div",
        }
    );
</script>
