<template>
    <main>
        <header class="flex items-center justify-between">
            <label>Conditions</label>
        </header>
        <ol class="flex w-full flex-col gap-2 rounded-lg p-2">
            <FormBase
                v-if="formState.showAddConditionForm"
                class="flex flex-col gap-2"
                v-slot="{ submitting }"
                :onSubmit="() => {}"
            >
                <div
                    class="flex w-full flex-wrap items-center gap-2 rounded-lg border border-dashed border-take-navy-light p-2"
                >
                    <FormInput
                        class="min-w-40 flex-1"
                        placeholder="Condition Name"
                        label="Name"
                        v-model:value="name"
                        :datalist="Dnd5eConditionList"
                        v-bind="nameInputProps"
                    />
                    <Dropdown
                        class="min-w-40 flex-1"
                        label="Type"
                        :items="[...durationTypeDropdownOptions]"
                        :displayFunc="(i) => i"
                        :keyFunc="(i) => i"
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
                        v-if="durationTypeDropdownRawValue == 'Turns'"
                        label="Triggering on"
                        class="min-w-40 flex-1"
                        :items="
                            [
                                ...roundTriggerTypeDropdownOptions,
                            ] as RoundTriggerTypeDurationOptions[]
                        "
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
                        v-bind="durationValueInputProps"
                    />
                    <FormInput
                        class="min-w-40 flex-1"
                        label="Note"
                        placeholder="Note"
                        v-model:value="note"
                        v-bind="noteInputProps"
                    />
                </div>
                <footer class="flex flex-row justify-between">
                    <FormButton size="sm" icon="plus" label="Add" />
                    <FormButton
                        size="sm"
                        icon="arrow-rotate-left"
                        buttonColour="take-charcoal"
                        @clicked="
                            () => (formState.showAddConditionForm = false)
                        "
                    />
                </footer>
            </FormBase>
            <li
                v-if="!formState.showAddConditionForm"
                :class="[
                    'cursor-pointer rounded-xl border-2 border-dashed border-take-navy-light p-1 text-center transition-colors hover:border-take-yellow',
                ]"
                @click="() => (formState.showAddConditionForm = true)"
            >
                + Add
            </li>
            <li v-for="condition in props.conditions">
                <CharacterConditionsDisplay />
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

// Setup
const props = defineProps<{
    conditions: Condition[] | undefined;
    error: string | null;
}>();

const emit = defineEmits<{
    (e: "addCondition", conditions: Condition): void;
    (e: "removeCondition", conditions: Condition): void;
}>();

// Form
const formState = reactive({
    showAddConditionForm: false,
});
const DurationOptionKeys = Object.keys(
    ConditionDurationTypes,
) as (keyof typeof ConditionDurationTypes)[];

const { defineField, validate } = useForm({
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
const roundTriggerTypeDropdownOptions = [
    "Top of Round",
    "Character's Turn",
] as const;
type RoundTriggerTypeDurationOptions =
    (typeof roundTriggerTypeDropdownOptions)[number];

const durationTypeDropdownRawValue = ref<DurationTypeDropdownOptions>("Rounds"); // Here is where the default is set.
const roundTriggerOptionRawValue =
    ref<RoundTriggerTypeDurationOptions>("Top of Round");
const onDurationTypeDropdownChanged = (value: DurationTypeDropdownOptions) => {
    durationTypeDropdownRawValue.value = value;
};

// Submission
async function addCondition() {
    const result = await validate();
    if (!result.valid) return;
}

defineExpose({
    
})
</script>
