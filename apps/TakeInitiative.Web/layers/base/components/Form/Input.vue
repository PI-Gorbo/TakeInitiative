<template>
    <div class="flex flex-col">
        <label
            :class="[`text-${props.textColour} block text-sm font-medium`]"
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
            v-bind="props.datalist ? { list: 'datalist' } : {}"
        />
        <label v-if="props.errorMessage != null" class="text-take-red">
            {{ props.errorMessage }}
        </label>
        <datalist v-if="props.datalist" id="datalist">
            <option v-for="item in props.datalist" :value="item" />
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
            datalist?: string[];
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
        datalist: undefined,
    },
);

const emits = defineEmits<{
    (e: "update:value", value: string | number | undefined): void;
}>();
</script>
