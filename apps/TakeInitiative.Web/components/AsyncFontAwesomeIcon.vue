<template>
    <FontAwesomeIcon
        v-bind="delegatedProps"
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

    const props = defineProps<
        FontAwesomeIconProps & {
            isLoading: boolean;
        }
    >();

    const delegatedProps = computed(() => {
        const { icon: _, ...delegated } = props;
        return delegated;
    });
</script>
