<template>
    <button
        class="btn h-min min-h-0 p-2"
        :class="[
            `bg-${buttonColour} text-${TakeInitContrastColour[props.buttonColour]} hover:bg-${hoverColour} disabled:bg-${disabledColour} disabled:text-take-navy disabled:hover:bg-take-grey`,
        ]"
        :type="props.type"
        @click="onClicked"
    >
        <FontAwesomeIcon
            :icon="iconToDisplay"
            :class="{ 'fa-spin': state.loading }"
        />
    </button>
</template>
<script setup lang="ts">
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
import type { TakeInitColour } from "base/utils/types/HelperTypes";
import { TakeInitContrastColour } from "base/utils/types/HelperTypes";
const props = withDefaults(
    defineProps<{
        icon: string;
        type: "button" | "submit";
        clicked?: () => Promise<void>;
        isLoading?: boolean;
        buttonColour: TakeInitColour;
        hoverButtonColour?: TakeInitColour;
        disabledColour?: TakeInitColour;
    }>(),
    {
        type: "button",
        isLoading: false,
        disabledColour: "take-grey",
    },
);

// State
const state = reactive({
    loading: false,
});
const isLoading = computed(() => state.loading || props.isLoading);
const iconToDisplay = computed(() =>
    isLoading.value ? "circle-notch" : "floppy-disk",
);
const hoverColour = computed(
    () => props.hoverButtonColour ?? props.buttonColour,
);

// Events
async function onClicked() {
    if (!props.clicked) {
        return;
    }

    if (state.loading == true) {
        return;
    }

    state.loading = true;
    await props.clicked().finally(() => (state.loading = false));
}
</script>
