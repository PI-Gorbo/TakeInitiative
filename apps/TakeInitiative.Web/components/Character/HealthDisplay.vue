<template>
    <div
        v-if="props.health?.hasHealth && shouldDisplay"
        class="flex select-none items-center gap-2"
    >
        <FontAwesomeIcon icon="droplet" />
        {{textToDisplay}}
    </div>
</template>
<script setup lang="ts">
import { DisplayOptionEnum, DisplayOptionValueMap, type CharacterHealth, type DisplayOptionValues } from "~/utils/types/models";
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
const props = withDefaults(
    defineProps<{
        health: CharacterHealth | null | undefined;
        displayMethod?: DisplayOptionValues;
    }>(),
    {
        displayMethod: DisplayOptionEnum['RealValue'],
    },
);
const shouldDisplay = computed(() => props.displayMethod != DisplayOptionEnum['Hidden'])
const textToDisplay = computed(() => {
    if (props.displayMethod == DisplayOptionEnum['RealValue']) {
        return `${props.health?.currentHealth} / ${props.health?.maxHealth}`
    } else if (props.displayMethod == DisplayOptionEnum['HealthyBloodied']) {
        const percentage = ((props.health?.currentHealth ?? 0) * 1.0) / ((props.health?.maxHealth ?? 1) * 1.0)
        console.log(percentage)
        if (percentage > 0.5) {
            return "Healthy"
        } else if (percentage <= 0.5 && percentage > 0.0) {
            return "Bloodied"
        } else {
            return "Dead"
        }
    }
})


</script>
