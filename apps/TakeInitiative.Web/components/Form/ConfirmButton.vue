<template>
    <FormButton v-bind="{ ...props }" :click="onClick" />
</template>
<script setup lang="ts">
import FormButton, { type LoadingDisplay } from "~/components/Form/Button.vue";
type FromButtonProps = InstanceType<typeof FormButton>["$props"];
type ConfirmButtonProps = FromButtonProps & {
    confirmText: string;
};
const props = withDefaults(defineProps<ConfirmButtonProps>(), {
    confirmText: "Click to Confirm",
});
const state = reactive<{
    waitingOnConfirmation: boolean;
    clickTriggered: boolean;
}>({
    waitingOnConfirmation: false,
    clickTriggered: false,
});

async function onClick() {

    if (state.clickTriggered == false && state.waitingOnConfirmation == false) {
        state.clickTriggered = true;
        state.waitingOnConfirmation = true;
        setTimeout(() => {
            if (state.waitingOnConfirmation == true && state.clickTriggered == true) {
                state.clickTriggered = false;
                state.waitingOnConfirmation = false;
            }
        }, 500)
    }

}

const buttonLabel : ComputedRef<string> = computed(() => {})
const loadingDisplay: ComputedRef<LoadingDisplay> = computed(() => {});
</script>
