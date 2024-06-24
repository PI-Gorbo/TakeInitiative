<template>
    <main>
        <label>Conditions</label>
        <ol
            class="flex w-full flex-col gap-2 rounded-lg border border-take-navy-light p-2"
        >
            <li v-for="condition in props.conditions">
                <CharacterConditionsDisplay />
            </li>
            <li
                class="flex w-full flex-wrap items-center gap-2 rounded-lg border border-dashed border-take-navy-light p-2"
            >
                <FormInput
                    class="min-w-40 flex-1"
                    placeholder="Condition Name"
                    label="Name"
                    v-model:value="name"
                    :datalist="Dnd5eConditionList"
                />
                <Dropdown
                    class="min-w-40 flex-1"
                    headerLabel="Duration"
                    :items="[...durationTypeDropdownOptions] as DurationTypeDropdownOptions[]"
                    :displayFunc="(i) => i"
                    :keyFunc="(item) => item"
                    :selectedItem="durationTypeDropdownRawValue"
                    :hoverOverContent="true"
                    colour="take-navy-light"
                    @update:selectedItem="
                        (v) =>
                            onDurationTypeDropdownChanged(
                                v as DurationTypeDropdownOptions,
                            )
                    "
                />
                <Dropdown
                    v-if="durationTypeDropdownRawValue == 'Rounds'"
                    class="min-w-40 flex-1"
                    headerLabel="Trigger on"
                    :items="[...roundTriggerTypeDropdownOptions] as RoundTriggerTypeDurationOptions[]"
                    :displayFunc="(i) => i"
                    :keyFunc="(item) => item"
                    v-model:selectedItem="roundTriggerOptionRawValue"
                    :hoverOverContent="true"
                    colour="take-navy-light"
                />
                <FormInput
                    class="min-w-20 flex-1"
                    label="Duration"
                    type="number"
                    v-model:value="durationValue"
                />
                <FormInput
                    class="min-w-40 flex-1"
                    label="Note"
                    placeholder="Notes"
                    v-model:value="note"
                />
            </li>
        </ol>
    </main>
</template>
<script setup lang="ts">
import {
    type Condition,
    ConditionDurationTypes,
    ConditionDurationTypesValues,
    conditionValidator,
} from "base/utils/types/models";
import { Dnd5eConditionList } from "base/utils/conditionData";
import { useForm } from "vee-validate";
import { toTypedSchema } from "@vee-validate/yup";
const DurationOptionKeys = Object.keys(
    ConditionDurationTypes,
) as (keyof typeof ConditionDurationTypes)[];

const { defineField } = useForm({
    validationSchema: toTypedSchema(conditionValidator),
});

const [name, nameInputProps] = defineField("name", {
    props: (state) => ({
        errorMessage: state.errors[0],
    }),
});
const [durationType, durationTypeInputProps] = defineField("duration.type", {
    props: (state) => ({
        errorMessage: state.errors[0],
    }),
});
const [durationValue, durationValueInputProps] = defineField("duration.value", {
    props: (state) => ({
        errorMessage: state.errors[0],
    }),
});
const [note, noteInputProps] = defineField("note", {
    props: (state) => ({
        errorMessage: state.errors[0],
    }),
});

// Condition Duration Type Dropdown
const durationTypeDropdownOptions = ["Rounds", "Turns", "Indefinite"] as const;
type DurationTypeDropdownOptions = (typeof durationTypeDropdownOptions)[number];
const roundTriggerTypeDropdownOptions = ['Top of Round', 'Character\'s Turn'] as const;
type RoundTriggerTypeDurationOptions = (typeof roundTriggerTypeDropdownOptions)[number];

const durationTypeDropdownRawValue = ref<DurationTypeDropdownOptions>('Rounds'); // Here is where the default is set.
const roundTriggerOptionRawValue = ref<RoundTriggerTypeDurationOptions>('Top of Round');
const onDurationTypeDropdownChanged = (value: DurationTypeDropdownOptions) => {
    durationTypeDropdownRawValue.value = value;
};

const props = defineProps<{
    conditions: Condition[] | undefined;
    error: string | null;
}>();

const emit = defineEmits<{
    (e: "addCondition", conditions: Condition): void;
    (e: "removeCondition", conditions: Condition): void;
}>();
</script>
