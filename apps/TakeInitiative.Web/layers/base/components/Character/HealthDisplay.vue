<template>
    <div v-if="shouldDisplay" class="flex select-none items-center gap-2">
        <FontAwesomeIcon icon="droplet" />
        {{ textToDisplay }}
    </div>
</template>
<script setup lang="ts">
import {
    HealthDisplayOptionsEnum,
    HealthDisplayOptionValueKeyMap,
    type UnevaluatedCharacterHealth,
    type HealthDisplayOptionValues,
    type CharacterHealth,
} from "base/utils/types/models";
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
const props = withDefaults(
    defineProps<{
        health: UnevaluatedCharacterHealth | CharacterHealth;
        displayMethod?: HealthDisplayOptionValues;
    }>(),
    {
        displayMethod: HealthDisplayOptionsEnum["RealValue"],
    },
);
const shouldDisplay = computed(
    () =>
        props.health["!"] != "None" &&
        props.displayMethod != HealthDisplayOptionsEnum["Hidden"],
);
const textToDisplay = computed(() => {
    if (props.health["!"] == "Roll") {
        switch (props.displayMethod) {
            case HealthDisplayOptionsEnum["RealValue"]:
                return props.health.rollString;
            default:
                return null;
        }
    }

    if (props.health["!"] == "Fixed") {
        if (props.displayMethod == HealthDisplayOptionsEnum["RealValue"]) {
            return `${props.health?.currentHealth} / ${props.health?.maxHealth}`;
        } else if (
            props.displayMethod == HealthDisplayOptionsEnum["HealthyBloodied"]
        ) {
            const percentage =
                ((props.health?.currentHealth ?? 0) * 1.0) /
                ((props.health?.maxHealth ?? 1) * 1.0);
            if (percentage > 0.5) {
                return "Healthy";
            } else if (percentage <= 0.5 && percentage > 0.0) {
                return "Bloodied";
            } else {
                return "Dead";
            }
        }
    }
});
</script>
