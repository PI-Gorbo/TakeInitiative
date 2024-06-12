<template>
    <Modal :title="props.title" ref="confirmModal">
        <div class="flex flex-col gap-4">
            <body v-if="props.bodyText" class="w-full text-center text-white">
                {{ props.bodyText }}
            </body>
            <div class="flex justify-around gap-4">
                <FormButton
                    :label="props.cancelText"
                    :buttonColour="props.cancelColour"
                    @clicked="cancel"
                    :loadingDisplay="{ showSpinner: true }"
                />
                <FormButton
                    autofocus
                    :label="props.confirmText"
                    :buttonColour="props.confirmColour"
                    @clicked="confirm"
                    :loadingDisplay="{ showSpinner: true }"
                />
            </div>
        </div>
    </Modal>
</template>
<script setup lang="ts">
import Modal from "base/components/Modal.vue";
import type { TakeInitColour } from "base/utils/types/HelperTypes";
import type { ButtonLoadingControl } from "./Form/Button.vue";
const confirmModal = ref<InstanceType<typeof Modal> | null>(null);
const props = withDefaults(
    defineProps<{
        title?: string;
        bodyText?: string;
        confirmText?: string;
        confirmColour?: TakeInitColour;
        closeOnConfirm?: boolean;
        cancelText?: string;
        cancelColour?: TakeInitColour;
        closeOnCancel?: boolean;
    }>(),
    {
        title: "Confirm",
        confirmText: "Confirm",
        confirmColour: "take-yellow",
        cancelText: "Cancel",
        cancelColour: "take-red",
        closeOnConfirm: true,
        closeOnCancel: true,
    },
);

const emit = defineEmits<{
    (e: "Confirm", loadingCtrl: ButtonLoadingControl): void;
    (e: "Cancel", loadingCtrl: ButtonLoadingControl): void;
}>();

function cancel(loadingCtrl: ButtonLoadingControl) {
    emit("Cancel", loadingCtrl);
    if (props.closeOnCancel) confirmModal.value?.hide();
}

function confirm(loadingCtrl: ButtonLoadingControl) {
    emit("Confirm", loadingCtrl);
    if (props.closeOnConfirm) confirmModal.value?.hide();
}

defineExpose({
    show: () => {
        confirmModal.value?.show();
    },
    hide: () => {
        confirmModal.value?.hide();
    },
});
</script>
