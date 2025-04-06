<template>
    <FontAwesomeIcon
        :icon="props.isLoading ? faCircleNotch : faCheck"
        :class="{
            'fa-spin': props.isLoading,
        }" />
</template>
<script setup lang="ts">
    import { faCheck, faCircleNotch } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    const props = defineProps<{
        isLoading: boolean;
    }>();
</script>
