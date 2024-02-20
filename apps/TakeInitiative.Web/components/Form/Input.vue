<template>
    <div class="flex flex-col">
        <label
            :class="[`text-${props.textColour} block text-sm font-medium`]"
            >{{ props.label }}</label
        >
        <input
            :autofocus="props.autoFocus"
            :class="[
                `md:text-md mt-1 w-full rounded-md bg-${props.colour} p-2 text-${props.textColour}`,
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
        />
        <label v-if="props.errorMessage != null" class="text-take-red">
            {{ props.errorMessage }}
        </label>
    </div>
</template>
<script setup lang="ts">
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
        }
    >(),
    {
        type: "input",
        placeholder: "",
        colour: "take-navy-light",
        textColour: "white",
        autoFocus: false,
    },
);

const emits = defineEmits<{
    (e: "update:value", value: string | number | undefined): void;
}>();
</script>
