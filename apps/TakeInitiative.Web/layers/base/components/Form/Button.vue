<template>
    <button
        ref="buttonRef"
        :name="props.label ?? props.icon"
        :class="[
            `btn flex h-min min-h-0 border-none`,
            props.disabled
                ? 'bg-take-grey-dark hover:bg-take-grey-dark'
                : `bg-${props.buttonColour} hover:bg-${props.hoverButtonColour} text-${props.textColour ?? TakeInitContrastColour[props.buttonColour]} hover:text-${$props.hoverTextColour ?? TakeInitContrastColour[props.hoverButtonColour]}`,
            size == null || 'sm' ? 'text-md p-3' : '',
            size == 'lg' ? 'text-md p-4' : '',
            size == 'xl' ? 'p-5 text-lg' : '',
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
                class="text-centre flex select-none items-center justify-center gap-1"
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
                <div
                    v-if="props.label"
                    class="cursor-pointer text-center"
                    @click="onClick"
                >
                    {{ props.label }}
                </div>
            </div>
            <div
                v-else-if="props.loadingDisplay"
                class="flex items-center justify-center gap-2"
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
} from "base/utils/types/HelperTypes";
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
import type { SubmittingState } from "./Base.vue";
import { TakeInitContrastColour } from "base/utils/types/HelperTypes";

const buttonRef = ref<HTMLButtonElement | null>(null);
export type LoadingDisplay = { showSpinner: true; loadingText?: string };
export type FromButtonProps = {
    name?: string;
    label?: string;
    loadingDisplay?: LoadingDisplay | null;
    isLoading?: SubmittingState | boolean | null;
    buttonColour?: TakeInitColour;
    hoverButtonColour?: TakeInitColour | undefined;
    textColour?: TakeInitColour | "base-200" | undefined;
    hoverTextColour?: TakeInitColour | undefined;
    icon?: string;
    size?: FontAwesomeIconSize;
    disabled?: boolean;
    click?: () => Promise<any | void>;
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

    state.isLoading = true;
    await props.click().finally(() => (state.isLoading = false));
}
const emit = defineEmits<{
    (e: "clicked", loadingCtrl: ButtonLoadingControl): void;
}>();
</script>
