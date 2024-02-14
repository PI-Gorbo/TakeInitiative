<template>
    <button
        :class="[
            `w-full px-4 text-lg transition-opacity bg-${props.buttonColour} my-2 rounded-md py-4 hover:bg-${props.hoverColour} text-${props.textColour}`,
            {
                ' bg-take-grey-dark hover:bg-take-grey-dark': props.disabled,
            },
        ]"
        type="submit"
        :disabled="props.disabled"
    >
        <slot>
            {{ props.isLoading ? props.loadingLabel : props.label }}
            <FontAwesomeIcon
                v-if="props.icon"
                :icon="['fas', props.icon]"
                :size="props.iconSize"
            />
        </slot>
    </button>
</template>

<script setup lang="ts">
import type {
    FrontAwesomeIconSize,
    TakeInitColour,
} from "~/utils/types/HelperTypes";
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";

const props = withDefaults(
    defineProps<{
        label?: string;
        loadingLabel?: string | null;
        isLoading?: boolean;
        buttonColour?: TakeInitColour;
        hoverColour?: TakeInitColour;
        textColour?: TakeInitColour;
        icon?: string;
        iconSize?: FrontAwesomeIconSize;
        disabled?: boolean;
    }>(),
    {
        label: undefined,
        loadingLabel: "Submitting",
        isLoading: false,
        buttonColour: "take-yellow-dark",
        hoverColour: "take-yellow-light",
        textColour: "take-navy-dark",
        icon: undefined,
        iconSize: "lg",
        disabled: false,
    },
);
</script>
