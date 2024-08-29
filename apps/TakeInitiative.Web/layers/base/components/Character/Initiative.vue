<template>
    <div>
        <header class="flex gap-2">
            <label class="text-sm">Initiative</label>
            <TooltipButton
                icon="circle-question"
                tooltip="Initiative is rolled when the character enters combat. It can either be a fixed value like '18', or a roll. An example of a roll is '1d20 + 2d4 + 3' which sums one 20 sided dice, two 4 sided die and adds 3."
            />
        </header>
        <FormInput
            class="flex-1"
            v-model:value="state.value"
            placeholder="Try '1d20 + 5' OR '18'"
            :errorMessage="error"
        />
    </div>
</template>
<script setup lang="ts">
import type { UnevaluatedCharacterInitiative } from "base/utils/types/models";

const props = withDefaults(
    defineProps<{
        initiative: UnevaluatedCharacterInitiative;
        errorMessage?: string | undefined;
    }>(),
    {},
);

const state = reactive<{
    value: string | undefined;
}>({
    value: props?.initiative?.value ?? "",
});

const error = computed(() => props.errorMessage);

defineExpose({
    getInitiative(): UnevaluatedCharacterInitiative | false {
        return {
            value: state.value ?? "",
        } satisfies UnevaluatedCharacterInitiative;
    },
});
</script>
