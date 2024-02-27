<template>
    <button
        :class="[
            `flex cursor-pointer justify-center rounded-md  transition-colors`,
            props.disabled
                ? 'bg-take-grey-dark hover:bg-take-grey-dark'
                : `bg-${props.buttonColour} hover:bg-${props.hoverButtonColour} text-${props.textColour} hover:text-${$props.hoverTextColour}`,
            size == 'sm' ? 'px-4 py-4 text-sm' : '',
            size != 'sm' ? 'px-4 py-4 text-lg' : '',
        ]"
        type="submit"
        :disabled="props.disabled"
        @click="emit('clicked', loadingControls)"
    >
        <slot>
            <div v-if="!props.isLoading" class="text-centre flex justify-center gap-1">
                <div
                    v-if="props.icon"
                    class="flex items-center justify-center text-center"
                >
                    <FontAwesomeIcon
                        v-if="props.icon"
                        :icon="props.icon"
                        :size="props.iconSize"
                    />
                </div>
                <label v-if="props.label" class="cursor-pointer">{{ props.label }}</label>
            </div>
            <div v-else-if="props.loadingDisplay" class="flex justify-center">
                <label
                    v-if="props.loadingLabel && typeof props.loadingDisplay === 'string'"
                    >{{ props.loadingLabel }}
                </label>
                <div
                    v-else-if="props.icon"
                    class="flex items-center justify-center text-center"
                >
                    <FontAwesomeIcon
                        class="fa-spin"
                        icon="circle-notch"
                        :size="props.size"
                    />
                </div>
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

export type LoadingDisplay = string | { showSpinner: true };
const props = withDefaults(
    defineProps<{
        label?: string;
        loadingDisplay?: LoadingDisplay | null;
        isLoading?: boolean;
        buttonColour?: TakeInitColour;
        hoverButtonColour?: TakeInitColour;
        textColour?: TakeInitColour;
        hoverTextColour?: TakeInitColour;
        icon?: string;
        size?: FontAwesomeIconSize;
        disabled?: boolean;
    }>(),
    {
        label: undefined,
        loadingDisplay: {
            showSpinner: true,
        },
        isLoading: false,
        buttonColour: "take-yellow-dark",
        hoverButtonColour: "take-yellow",
        textColour: "take-navy-dark",
        hoverTextColour: "take-navy-dark",
        icon: undefined,
        iconSize: "lg",
        disabled: false,
    }
);

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

const emit = defineEmits<{
    (e: "clicked", loadingCtrl: ButtonLoadingControl): void;
}>();
</script>
