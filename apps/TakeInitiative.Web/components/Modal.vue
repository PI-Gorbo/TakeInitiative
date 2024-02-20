<template>
    <dialog
        ref="dialog"
        @click="onModalClick"
        class="rounded-xl bg-take-navy-medium p-5"
    >
        <div class="flex gap-4 pb-5">
            <h1
                v-if="props.title"
                class="flex w-max items-center text-xl text-white"
            >
                {{ props.title }}
            </h1>
            <div class="flex flex-1 justify-end">
                <FormButton
                    class=""
                    icon="xmark"
                    @clicked="hide"
                    size="sm"
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
    const element = event.target as HTMLDialogElement;
    if (element != dialog.value) {
        return;
    }

    var rect = element.getBoundingClientRect();
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

<style>
::backdrop {
    background-color: black;
    opacity: 0.75;
}
</style>
