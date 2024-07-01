<template>
    <div class="tooltip overflow-visible" :data-tip="props.tooltip">
        <FontAwesomeIcon :icon="props.icon" />
    </div>
</template>
<script setup lang="ts">
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";

const props = defineProps<{
    icon: string;
    tooltip: string;
}>();
</script>
