<template>
    <main>
        <label>Conditions</label>
        <ol
            class="flex w-full flex-col gap-2 rounded-lg border border-take-navy-light p-2"
        >
            <li v-for="condition in props.conditions"></li>
            <li
                class="flex w-full items-center rounded-lg border border-dashed border-take-navy-light p-2"
            >
                <FormInput
                    label="Name"
                    v-model:value="newConditionState.name"
                    :datalist="Dnd5eConditionList"
                />
                <Dropdown
                    headerLabel="Duration Type"
                    :items="DurationOptionKeys"
                    :displayFunc="(item) => item"
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
                    label="Duration"
                    type="number"
                    v-model:value="newConditionState.duration.value"
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
} from "base/utils/types/models";
import { Dnd5eConditionList } from "base/utils/conditionData";
const DurationOptionKeys = Object.keys(
    ConditionDurationTypes,
) as (keyof typeof ConditionDurationTypes)[];
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
    (e: "update:conditions", conditions: Condition[]): void;
}>();
</script>
