<template>
    <FormInput
        label="Armour Class (Optional)"
        type="number"
        :value="props.value"
        @update:value="updateValue"
        :class="[props.value == null && 'brightness-75']"
    />
</template>
<script setup lang="ts">
const props = defineProps<{
    value: number | null | undefined;
}>();

const emit = defineEmits<{
    (e: "update:value", value: number | null): void;
}>();

function updateValue(value: string | number | undefined) {
    if (!value) {
        emit("update:value", null);
    } else {
        emit("update:value", Number(value));
    }
}
</script>
