<template>
    <button
        :class="[
            `w-full px-4 text-lg bg-${props.buttonColour} my-2 rounded-md py-4 hover:bg-${props.hoverColour} text-${props.textColour}`,
        ]"
        type="submit"
    >
        {{ props.isLoading ? props.loadingLabel : props.label }}
    </button>
</template>

<script setup lang="ts">
import type { TakeInitColour } from "~/utils/types/HelperTypes";

const props = withDefaults(
    defineProps<{
        label?: string;
        loadingLabel?: string | null;
        isLoading?: boolean;
        buttonColour?: TakeInitColour;
        hoverColour?: TakeInitColour;
        textColour?: TakeInitColour;
    }>(),
    {
        label: "Submit",
        loadingLabel: "Submitting",
        isLoading: false,
        buttonColour: "take-yellow-dark",
        hoverColour: "take-yellow-light",
        textColour: "take-navy-dark",
    }
);
</script>
