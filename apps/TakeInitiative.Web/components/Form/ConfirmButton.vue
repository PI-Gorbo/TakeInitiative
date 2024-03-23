<template>
    <FormButton
        :click="onClick"
        :label="buttonLabel"
        :icon="props.icon"
        :buttonColour="props.buttonColour"
        :size="props.size"
        :hoverButtonColour="hoverColour"
        :loadingDisplay="props.loadingDisplay"
    />
</template>
<script setup lang="ts">
import type { TakeInitColour } from "~/utils/types/HelperTypes";
import type { FromButtonProps } from "./Button.vue";

type ConfirmButtonProps = FromButtonProps & {
    confirmText?: string;
    confirmHoverColour?: TakeInitColour;
};
const props = withDefaults(defineProps<ConfirmButtonProps>(), {
    confirmText: "Click to Confirm",
    confirmHoverColour: "take-red",
});

const state = reactive<{
    waitingOnConfirmation: boolean;
    timeoutId: NodeJS.Timeout | null;
}>({
    waitingOnConfirmation: false,
    timeoutId: null,
});

async function onClick() {
    if (state.waitingOnConfirmation == false) {
        state.waitingOnConfirmation = true;
        // Set a timeout to reset the state of the button
        state.timeoutId = setTimeout(() => {
            if (state.waitingOnConfirmation == true) {
                state.waitingOnConfirmation = false;
                state.timeoutId = null;
            }
        }, 3000);
        return;
    }

    if (state.timeoutId) {
        clearTimeout(state.timeoutId);
        state.timeoutId = null;
        state.waitingOnConfirmation = false;
    }

    if (props.click) {
        return await props.click();
    }
}

const emit = defineEmits<{
    (e: "clicked"): void;
}>();

const buttonLabel: ComputedRef<string> = computed(() =>
    state.waitingOnConfirmation ? props.confirmText! : props.label!,
);

const hoverColour: ComputedRef<TakeInitColour | undefined> = computed(
    () =>
        (state.waitingOnConfirmation
            ? props.confirmHoverColour
            : props.hoverButtonColour) ?? props.hoverButtonColour,
);
</script>
