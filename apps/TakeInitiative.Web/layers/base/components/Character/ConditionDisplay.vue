<template>
    <li
        :class="[
            `bg-${getConditionBackgroundColour(props.conditionName)} text-${TakeInitContrastColour[getConditionBackgroundColour(props.conditionName)]} flex items-center gap-1 rounded p-1`,
        ]"
    >
        <FontAwesomeIcon :icon="getConditionIcon(props.conditionName)" />
        {{ props.conditionName }}
    </li>
</template>
<script lang="ts" setup>
import { TakeInitContrastColour } from "base/utils/types/HelperTypes";
const props = defineProps<{
    conditionName: string;
}>();
</script>
