<template>
    <div>
        <header class="flex gap-2">
            <label class="text-sm">Initiative</label>
            <TooltipButton
                icon="circle-question"
                tooltip="Initiative is rolled when the character enters combat. It can either be a fixed value, or a roll. An example of a roll is 1d20 + 2d4 + 3 which sums one 20 sided dice and two 4 sided die, then adds 3."
            />
        </header>
        <div class="flex flex-row">
            <select
                name="Initiative Strategy"
                :value="props.initiativeStrategy"
                @input="
                    (e: Event) =>
                        emit(
                            'update:initiativeStrategy',
                            Number((e.target as HTMLSelectElement).value),
                        )
                "
                class="rounded-l-lg bg-take-grey-dark py-2 pl-2 pr-1 text-take-navy"
            >
                <option :value="InitiativeStrategy.Fixed">Fixed</option>
                <option :value="InitiativeStrategy.Roll">Roll</option>
            </select>
            <input
                type="text"
                class="flex-1 rounded-r-lg bg-take-navy-light px-1 text-white"
                :value="props.initiativeValue"
                @input="
                    (e) =>
                        emit(
                            'update:initiativeValue',
                            (e.target as HTMLInputElement).value,
                        )
                "
                :placeholder="
                    props.initiativeStrategy == InitiativeStrategy.Fixed
                        ? '+5'
                        : '1d20 + 5'
                "
            />
        </div>
        <label v-if="props.errorMessage" class="text-take-red">{{
            props.errorMessage
        }}</label>
    </div>
</template>
<script setup lang="ts">
import { InitiativeStrategy } from "base/utils/types/models";

const props = withDefaults(
    defineProps<{
        initiativeStrategy?: InitiativeStrategy;
        initiativeValue?: string;
        errorMessage?: string;
    }>(),
    {},
);

const emit = defineEmits<{
    (
        e: "update:initiativeStrategy",
        initiativeStrategy: InitiativeStrategy,
    ): void;
    (e: "update:initiativeValue", initiativeValue: string): void;
}>();
</script>
