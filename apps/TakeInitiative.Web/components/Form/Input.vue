<template>
    <div class="flex flex-col">
        <label class="block text-sm font-medium">{{ props.label }}</label>
        <input
            class="text-white mt-1 p-2 w-full md:text-md rounded-md bg-take-navy-light"
            :value="props.value"
            @input="(event) => emits('update:value', (event.target as HTMLInputElement).value)"
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

const props = withDefaults(
    defineProps<
        FormInputProps<string | undefined> & {
            type?: string;
            placeholder?: string;
        }
    >(),
    {
        type: "input",
        placeholder: "",
    }
);

const emits = defineEmits<{
    (e: "update:value", value: string | undefined): void;
}>();
</script>
