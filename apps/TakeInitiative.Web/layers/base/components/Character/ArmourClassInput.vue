<template>
    <div class="flex items-center gap-2">
        <FormInput
            class="flex-1"
            label="Armour Class (Optional)"
            type="number"
            :value="props.value"
            placeholder="Eg: 15"
            @update:value="updateValue"
        />
        <div class="pt-5">
            <FormIconButton
                class="p-3"
                icon="arrow-rotate-left"
                buttonColour="take-purple-light"
                @clicked="() => updateValue(undefined)"
            />
        </div>
    </div>
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
