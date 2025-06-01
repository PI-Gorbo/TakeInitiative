<template>
    <FontAwesomeIcon
        v-show="props.isLoading || isShowingOutcomeIcon"
        :icon="
            props.isLoading ? faCircleNotch : props.isSuccess ? faCheck : faX
        "
        :class="{
            'fa-spin': props.isLoading,
            'text-destructive': !props.isSuccess,
        }" />
</template>
<script setup lang="ts">
    import {
        faCheck,
        faCircleNotch,
        faX,
    } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import { useTimeoutFn } from "@vueuse/core";
    const props = withDefaults(
        defineProps<{
            isLoading: boolean;
            isSuccess?: boolean;
        }>(),
        {
            isSuccess: true,
        }
    );

    const isShowingOutcomeIcon = ref(false);
    const showOutcomeIcon = useTimeoutFn(() => {
        isShowingOutcomeIcon.value = false;
        showOutcomeIcon.stop();
    }, 1000);

    watch(
        () => props.isLoading,
        (newValue, oldValue) => {
            if (newValue === false && oldValue === true) {
                // Just finished loading.
                // Show the success / failure tick for a short time.
                isShowingOutcomeIcon.value = true;
                showOutcomeIcon.start();
            }
        }
    );
</script>
