<template>
    <dialog
        ref="dialog"
        @click="onModalClick"
        class="w-1/2 h-1/2 bg-take-navy-light rounded-xl p-2"
    >
        <div class="flex mx-1 my-1">
            <h1 v-if="props.title" class="text-xl text-white flex items-center">
                {{ props.title }}
            </h1>
            <div class="flex flex-1 justify-end">
                <FormButton
                    class=""
                    icon="xmark"
                    @clicked="hide"
                    iconSize="sm"
                    buttonColour="take-navy-light"
                    hoverButtonColour="take-yellow"
                    hoverTextColour="take-grey"
                />
            </div>
        </div>
        <slot />
    </dialog>
</template>
<script setup lang="ts">
const dialog = ref<HTMLDialogElement | null>(null);

const props = defineProps<{
    title?: string;
}>();

const emits = defineEmits<{
    (e: "ShowModal"): void;
    (e: "HideModal"): void;
}>();

function onModalClick(event: MouseEvent) {
    const modal = event.target as HTMLDialogElement;
    var rect = modal.getBoundingClientRect();
    var isInDialog =
        rect.top <= event.clientY &&
        event.clientY <= rect.top + rect.height &&
        rect.left <= event.clientX &&
        event.clientX <= rect.left + rect.width;
    if (!isInDialog) {
        hide();
    }
}

const show = () => {
    dialog.value?.showModal();
    emits("ShowModal");
};

const hide = () => {
    dialog.value?.close();
    emits("HideModal");
};

defineExpose({
    show,
    hide,
});
</script>
