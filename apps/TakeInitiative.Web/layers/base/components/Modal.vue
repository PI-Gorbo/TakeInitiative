<template>
    <dialog
        ref="dialog"
        @click="onModalClick"
        class="mt-20 w-full rounded-xl border border-take-yellow-dark bg-take-purple-very-dark p-5 sm:w-full md:w-3/4 lg:w-1/2"
    >
        <div class="my-1 flex gap-4">
            <h1
                v-if="props.title"
                class="flex w-max items-center font-NovaCut text-xl text-white"
            >
                {{ props.title }}
            </h1>
            <div class="flex flex-1 justify-end">
                <FormButton
                    class="p-0"
                    size="lg"
                    icon="xmark"
                    @clicked="hide"
                    buttonColour="take-navy"
                    hoverButtonColour="take-navy-medium"
                    hoverTextColour="take-grey"
                />
            </div>
        </div>
        <slot :key="id" />
        <div class="flex flex-1 justify-end py-4"></div>
    </dialog>
</template>
<script setup lang="ts">
const { isMobile } = useDevice();
const dialog = ref<HTMLDialogElement | null>(null);
const id = ref<number>(0);
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
    id.value = id.value + 1;
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
