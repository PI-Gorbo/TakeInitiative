<template>
    <FontAwesomeIcon
        v-if="props.state != 'Idle'"
        :icon="
            props.state === 'TriggeredAndWaiting'
                ? faCircleNotch
                : props.state === 'Success'
                  ? faCheck
                  : faX
        "
        :class="{
            'fa-spin': props.state === 'TriggeredAndWaiting',
        }" />
</template>
<script setup lang="ts">
    import {
        faCheck,
        faCircleNotch,
        faX,
    } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";

    const props = defineProps<{
        state: "Idle" | "TriggeredAndWaiting" | "Success" | "Failure";
    }>();
</script>
