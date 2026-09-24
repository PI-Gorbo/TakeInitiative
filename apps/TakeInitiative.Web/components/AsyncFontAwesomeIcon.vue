<template>
    <FontAwesomeIcon
        :icon="props.isLoading ? faCircleNotch : props.icon"
        :class="{
            'fa-spin': props.isLoading,
        }" />
</template>
<script setup lang="ts">
    import { faCircleNotch } from "@fortawesome/free-solid-svg-icons";
    import {
        FontAwesomeIcon,
        type FontAwesomeIconProps,
    } from "@fortawesome/vue-fontawesome";

    // Other FontAwesomeIcon props fall through as attrs.
    const props = defineProps<{
        icon: FontAwesomeIconProps["icon"];
        isLoading: boolean;
    }>();
</script>
