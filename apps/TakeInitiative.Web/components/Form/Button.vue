<template>
    <button
        ref="buttonRef"
        :name="props.label ?? props.icon"
        :class="[
            `flex cursor-pointer justify-center rounded-md  transition-colors`,
            props.disabled
                ? 'bg-take-grey-dark hover:bg-take-grey-dark'
                : `bg-${props.buttonColour} hover:bg-${props.hoverButtonColour} text-${props.textColour} hover:text-${$props.hoverTextColour}`,
            size == 'sm' ? 'px-2.5 py-2.5 text-sm' : '',
            size != 'sm' ? 'px-4 py-4 text-lg' : '',
        ]"
        type="submit"
        :disabled="props.disabled"
        @click="onClick"
    >
        <slot>
            <div
                v-if="
                    !props.isLoading ||
                    (props.isLoading &&
                        typeof props.isLoading == 'object' &&
                        props.isLoading.submitterId != buttonRef?.id)
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
                        :size="props.iconSize"
                    />
                </div>
                <div v-if="props.label" class="cursor-pointer" @click="onClick">
                    {{ props.label }}
                </div>
            </div>
            <div v-else-if="props.loadingDisplay" class="flex justify-center">
                <label v-if="typeof props.loadingDisplay === 'string'"
                    >{{ props.loadingDisplay }}
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
import type { SubmittingState } from "./Base.vue";

const buttonRef = ref<HTMLButtonElement | null>(null);
export type LoadingDisplay = string | { showSpinner: true };
const props = withDefaults(
    defineProps<{
        name?: string;
        label?: string;
        loadingDisplay?: LoadingDisplay | null;
        isLoading?: SubmittingState | boolean | null;
        buttonColour?: TakeInitColour;
        hoverButtonColour?: TakeInitColour;
        textColour?: TakeInitColour;
        hoverTextColour?: TakeInitColour;
        icon?: string;
        size?: FontAwesomeIconSize;
        disabled?: boolean;
        click?: () => Promise<any>;
    }>(),
    {
        name: undefined,
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
        click: undefined,
    },
);
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
