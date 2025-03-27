<template>
    <Badge
        v-if="shouldDisplay"
        variant="outline"
        :class="[
            'flex gap-1',
            {
                [`bg-orange-400 hover:bg-orange-400  text-${TakeInitContrastColour['take-cream']}`]:
                    healthDisplayState.state == 'Bloodied',
                'bg-take-teal hover:bg-take-teal':
                    healthDisplayState.state == 'Healthy',
                'bg-take-red hover:bg-take-red':
                    healthDisplayState.state == 'Dead',
            },
        ]">
        <FontAwesomeIcon :icon="faDroplet" />
        <span>{{ healthDisplayState?.labelValue }}</span>
    </Badge>
</template>
<script setup lang="ts">
    import {
        HealthDisplayOptionsEnum,
        type UnevaluatedCharacterHealth,
        type HealthDisplayOptionValues,
        type CharacterHealth,
    } from "~/utils/types/models";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import { TakeInitContrastColour } from "~/utils/types/HelperTypes";
    import Badge from "~/components/ui/badge/Badge.vue";
    import { faDroplet } from "@fortawesome/free-solid-svg-icons";
    const props = withDefaults(
        defineProps<{
            health: UnevaluatedCharacterHealth | CharacterHealth;
            displayMethod?: HealthDisplayOptionValues;
        }>(),
        {
            displayMethod: HealthDisplayOptionsEnum["RealValue"],
        }
    );
    const shouldDisplay = computed(
        () =>
            props.health["!"] != "None" &&
            props.displayMethod != HealthDisplayOptionsEnum["Hidden"]
    );
    type HealthDisplayState = {
        type: (UnevaluatedCharacterHealth | CharacterHealth)["!"];
        labelValue: string | null;
        state: "Healthy" | "Bloodied" | "Dead" | null;
    };
    const healthDisplayState: ComputedRef<HealthDisplayState> = computed(() => {
        if (props.health["!"] == "Roll") {
            switch (props.displayMethod) {
                case HealthDisplayOptionsEnum["RealValue"]:
                    return {
                        type: props.health["!"],
                        labelValue: props.health.rollString,
                        state: null,
                    } as HealthDisplayState;
                default:
                    return {
                        type: props.health["!"],
                        labelValue: null,
                        state: null,
                    } as HealthDisplayState;
            }
        }

        if (props.health["!"] == "Fixed") {
            const percentage =
                ((props.health?.currentHealth ?? 0) * 1.0) /
                ((props.health?.maxHealth ?? 1) * 1.0);
            const state =
                percentage > 0.5
                    ? "Healthy"
                    : percentage > 0.0
                      ? "Bloodied"
                      : "Dead";
            if (props.displayMethod == HealthDisplayOptionsEnum["RealValue"]) {
                return {
                    type: props.health["!"],
                    labelValue: `${props.health?.currentHealth} / ${props.health?.maxHealth}`,
                    state,
                } as HealthDisplayState;
            } else if (
                props.displayMethod ==
                HealthDisplayOptionsEnum["HealthyBloodied"]
            ) {
                return {
                    type: props.health["!"],
                    labelValue: state,
                    state,
                } as HealthDisplayState;
            }
        }

        throw new Error("Invalid health state.");
    });
</script>
