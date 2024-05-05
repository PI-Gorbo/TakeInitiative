<template>
    <button
        ref="buttonRef"
        :name="props.label ?? props.icon"
        :class="[
            `flex cursor-pointer justify-center rounded-md  transition-colors`,
            props.disabled
                ? 'bg-take-grey-dark hover:bg-take-grey-dark'
                : `bg-${props.buttonColour} hover:bg-${props.hoverButtonColour} text-${props.textColour ?? TakeInitContrastColour[props.buttonColour]} hover:text-${$props.hoverTextColour ?? TakeInitContrastColour[props.hoverButtonColour]}`,
            size == 'sm' ? 'px-2.5 py-2.5 text-sm' : '',
            size == 'md' ? 'text-md px-3 py-3' : '',
            size == 'lg' ? 'px-4 py-4 text-lg' : '',
        ]"
        type="submit"
        :disabled="props.disabled"
        @click="onClick"
    >
        <slot>
            <div
                v-if="
                    !isLoading ||
                    props.loadingDisplay == null ||
                    (isLoading &&
                        typeof props.isLoading == 'object' &&
                        props.isLoading?.submitterId != buttonRef?.id)
                "
                class="text-centre flex select-none justify-center gap-1"
            >
                <div
                    v-if="props.icon"
                    class="flex items-center justify-center text-center"
                >
                    <FontAwesomeIcon
                        v-if="props.icon"
                        :icon="props.icon"
                        :size="props.size"
                    />
                </div>
                <div v-if="props.label" class="cursor-pointer" @click="onClick">
                    {{ props.label }}
                </div>
            </div>
            <div
                v-else-if="props.loadingDisplay"
                class="flex justify-center gap-2"
            >
                <div
                    v-if="props.loadingDisplay.showSpinner"
                    class="flex items-center justify-center text-center"
                >
                    <FontAwesomeIcon
                        class="fa-spin"
                        icon="circle-notch"
                        :size="props.size"
                    />
                </div>
                <label v-if="props.loadingDisplay.loadingText"
                    >{{ props.loadingDisplay.loadingText }}
                </label>
            </div>
        </slot>
    </button>
</template>

<script setup lang="ts">
import type {
    FontAwesomeIconSize as FontAwesomeIconSize,
    TakeInitColour,
} from "~/utils/types/HelperTypes";
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
import type { SubmittingState } from "./Base.vue";
import { TakeInitContrastColour } from "~/utils/types/HelperTypes";

const buttonRef = ref<HTMLButtonElement | null>(null);
export type LoadingDisplay = { showSpinner: true; loadingText?: string };
export type FromButtonProps = {
    name?: string;
    label?: string;
    loadingDisplay?: LoadingDisplay | null;
    isLoading?: SubmittingState | boolean | null;
    buttonColour?: TakeInitColour;
    hoverButtonColour?: TakeInitColour | undefined;
    textColour?: TakeInitColour;
    hoverTextColour?: TakeInitColour | undefined;
    icon?: string;
    size?: FontAwesomeIconSize | "md";
    disabled?: boolean;
    click?: () => Promise<any>;
    preventClickBubbling?: boolean;
};

const props = withDefaults(defineProps<FromButtonProps>(), {
    name: undefined,
    label: undefined,
    loadingDisplay: {
        showSpinner: true,
        loadingText: "",
    },
    isLoading: false,
    buttonColour: "take-yellow-dark",
    hoverButtonColour: "take-yellow",
    textColour: undefined,
    hoverTextColour: undefined,
    icon: undefined,
    disabled: false,
    click: undefined,
    preventClickBubbling: true,
});
const buttonName = computed(() => props.name ?? props.label);

const state = reactive({
    isLoading: false,
});
const isLoading = computed(() => props.isLoading || state.isLoading);
const loadingControls = {
    setLoading: () => {
        state.isLoading = true;
    },
    setLoaded: () => {
        state.isLoading = false;
    },
};

export type ButtonLoadingControl = typeof loadingControls;
async function onClick(event: Event) {
    if (props.preventClickBubbling) {
        event.stopPropagation();
    }

    if (isLoading.value) {
        return;
    }

    emit("clicked", loadingControls);
    if (props.click == undefined) {
        return;
    }

    console.log("calling the on click method");
    state.isLoading = true;
    await props.click().finally(() => (state.isLoading = false));
}
const emit = defineEmits<{
    (e: "clicked", loadingCtrl: ButtonLoadingControl): void;
}>();
</script>
