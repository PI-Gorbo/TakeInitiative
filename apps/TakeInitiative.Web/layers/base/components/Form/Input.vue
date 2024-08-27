<template>
    <div class="flex flex-col">
        <label
            v-if="props.label"
            :class="[`text-${props.textColour} block text-sm font-medium`]"
            list="suggestions"
            >{{ props.label }}</label
        >
        <input
            :autofocus="props.autoFocus"
            :class="[
                `md:text-md mt-1 w-full rounded-md bg-${props.colour} p-2 disabled:brightness-75 text-${props.textColour}`,
            ]"
            :value="props.value"
            @input="
                (event) =>
                    emits(
                        'update:value',
                        (event.target as HTMLInputElement).value,
                    )
            "
            :type="props.type"
            :placeholder="props.placeholder"
            :disabled="props.disabled"
        />
        <label v-if="props.errorMessage != null" class="text-take-red">
            {{ props.errorMessage }}
        </label>
        <datalist v-if="props.suggestions" id="suggestions">
            <option v-for="(opt, index) in props.suggestions" :key="index">
                {{ opt }}
            </option>
        </datalist>
    </div>
</template>
<script setup lang="ts">
import type { FormInputProps } from "base/utils/types/FormInputBase";
import type { TakeInitColour } from "base/utils/types/HelperTypes";

const props = withDefaults(
    defineProps<
        FormInputProps<string | number | undefined | null> & {
            type?: string;
            placeholder?: string;
            colour?: TakeInitColour;
            textColour?: TakeInitColour | "white";
            autoFocus?: boolean;
            disabled?: boolean;
            suggestions?: string[] | undefined;
        }
    >(),
    {
        type: "input",
        placeholder: "",
        colour: "take-navy-light",
        textColour: "white",
        autoFocus: false,
        errorMessage: null,
        disabled: false,
        suggestions: undefined,
    },
);

const emits = defineEmits<{
    (e: "update:value", value: string | number | undefined): void;
}>();
</script>
