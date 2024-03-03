<template>
    <div class="flex flex-col" :class="[`text-${props.textColour}`]">
        <label :class="[`block text-sm`]">{{ props.label }}</label>
        <form
            @submit.prevent
            @submit="onNotEditableClicked"
            v-if="
                !props.enableToggleEditable ||
                (props.enableToggleEditable && state.editable)
            "
            :class="[
                `md:text-md flex items-center rounded-md bg-${props.colour} gap-2 text-${props.textColour}`,
            ]"
        >
            <div
                :class="[
                    `md:text-md h-full w-min rounded-md py-2 pl-2 bg-${props.colour} text-${props.textColour}`,
                ]"
            >
                <input
                    :autofocus="props.autoFocus"
                    :value="props.value"
                    :class="[
                        `md:text-md w- h-full rounded-sm bg-${props.colour} text-${props.textColour}`,
                    ]"
                    @input="
                        (event) => {
                            emits(
                                'update:value',
                                (event.target as HTMLInputElement).value,
                            );
                          
                        }
                    "
                    :type="props.type"
                    :placeholder="props.placeholder"
                />
            </div>
            <FontAwesomeIcon
                v-if="props.enableToggleEditable"
                :icon="!state.loading ? 'save' : 'circle-notch'"
                :class="['cursor-pointer pr-2', state.loading ? 'fa-spin' : '']"
                @click="onNotEditableClicked"
            />
        </form>
        <label
            v-else
            :class="[
                `md:text-md sw-full flex rounded-md bg-${props.notEditableColour} p-2 text-${props.textColour} flex items-center gap-2`,
            ]"
        >
            <div class="flex-1">{{ props.value }}</div>
            <FontAwesomeIcon
                icon="pencil"
                class="cursor-pointer"
                @click="state.editable = true"
            />
        </label>
        <label v-if="props.errorMessage != null" class="text-take-red">
            {{ props.errorMessage }}
        </label>
    </div>
</template>
<script setup lang="ts">
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
import type { FormInputProps } from "~/utils/types/FormInputBase";
import type { TakeInitColour } from "~/utils/types/HelperTypes";

const props = withDefaults(
    defineProps<
        FormInputProps<string | number | undefined> & {
            type?: string;
            placeholder?: string;
            colour?: TakeInitColour;
            textColour?: TakeInitColour | "white";
            autoFocus?: boolean;
            enableToggleEditable?: boolean;
            notEditableColour?: TakeInitColour;
            onNotEditable?: () => Promise<any>;
        }
    >(),
    {
        type: "input",
        placeholder: "",
        colour: "take-navy-light",
        textColour: "white",
        autoFocus: false,
        errorMessage: null,
        enableToggleEditable: true,
        notEditableColour: "take-navy-light",
    }
);

const state = reactive({
    editable: false,
    loading: false,
});

const emits = defineEmits<{
    (e: "update:value", value: string | number | undefined): void;
}>();

async function onNotEditableClicked() {
    if (props.onNotEditable) {
        state.loading = true;
        await props.onNotEditable().finally(() => (state.loading = false));
    }
    state.editable = false;
}
</script>
