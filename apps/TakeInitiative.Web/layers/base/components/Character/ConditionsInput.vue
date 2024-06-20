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
                    class="flex-1 min-w-40"
                    placeholder="Condition Name"
                    label="Name"
                    v-model:value="newConditionState.name"
                    :datalist="Dnd5eConditionList"
                />
                <Dropdown
                    class="flex-1 min-w-40"
                    headerLabel="Trigger"
                    :items="DurationOptionKeys"
                    :displayFunc="
                        (item) => {
                            if (item == 'Rounds_OnCharacterTurn') {
                                return 'Character\'s Turn';
                            } else if (item == 'Rounds_StartOfRound') {
                                return 'Start of Round';
                            }
                            return item;
                        }
                    "
                    :keyFunc="(item) => item"
                    :selectedItem="
                        ConditionDurationTypesValues[
                            newConditionState.duration.type
                        ]
                    "
                    :hoverOverContent="true"
                    colour="take-navy-light"
                    @update:selectedItem="
                        (update) =>
                            (newConditionState.duration.type =
                                ConditionDurationTypes[update])
                    "
                />
                <FormInput
                    class="flex-1 min-w-20"
                    label="Duration"
                    type="number"
                    v-model:value="newConditionState.duration.value"
                />
                <FormInput
                    class="flex-1 min-w-40"
                    label="Note"
                    placeholder="Notes"
                    v-model:value="newConditionState.note"
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
const DurationOptionKeys = Object.keys(
    ConditionDurationTypes,
) as (keyof typeof ConditionDurationTypes)[];

const formState = reactive<{
    formError: ApiError<Condition> | undefined | null,
}>({
    formError: undefined
})
const {defineField} = useForm({
    validationSchema: conditionValidator.required()
})
const [name, nameInputProps] = defineField('name',{
    props: (state) => ({
        errorMessage:
            formState.formError == null
                ? state.errors[0]
                : formState.formError?.getErrorFor("campaignName"),
    }),
})



const newConditionState = reactive<Condition>({
    name: "",
    duration: {
        type: 0,
        value: 1,
    },
    note: null,
});

const props = defineProps<{
    conditions: Condition[] | undefined;
    error: string | null;
}>();

const emit = defineEmits<{
    (e: "addCondition", conditions: Condition): void;
    (e: "removeCondition", conditions: Condition): void;
}>();
</script>
