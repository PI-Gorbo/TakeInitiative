<template>
    <main>
        <section class="flex justify-between">
            <label>Conditions</label>
        </section>

        <ul class="flex flex-col gap-2 px-1">
            <Collapsable>
                <template #Header>
                    <section>
                        <FontAwesomeIcon
                            :icon="faClockRotateLeft"
                            class="text-white"
                        />
                        Defaults
                    </section>
                </template>
                <template #Content>
                    <ul class="flex flex-col gap-2">
                        <li
                            v-for="(
                                defaultCondition, index
                            ) in DefaultConditions"
                            :key="index"
                            class="flex"
                        >
                            <label
                                class="flex flex-1 items-center transition-colors"
                                :class="{
                                    'text-take-grey':
                                        !defaultConditionsEnabled[
                                            defaultCondition.name
                                        ],
                                }"
                                >{{ defaultCondition.name }}</label
                            >
                            <input
                                type="checkbox"
                                class="toggle"
                                :class="{
                                    'bg-take-yellow':
                                        defaultConditionsEnabled[
                                            defaultCondition.name
                                        ],
                                    'hover:bg-take-yellow-dark':
                                        defaultConditionsEnabled[
                                            defaultCondition.name
                                        ],
                                }"
                                @input="
                                    () =>
                                        defaultConditionToggled(
                                            defaultCondition.name,
                                        )
                                "
                                :checked="
                                    defaultConditionsEnabled[
                                        defaultCondition.name
                                    ]
                                "
                            />
                        </li>
                    </ul>
                </template>
            </Collapsable>
            <Collapsable>
                <template #Header>
                    <section>
                        <FontAwesomeIcon
                            :icon="faPaintBrush"
                            class="text-white"
                        />
                        Custom
                    </section>
                </template>
                <template #Content>
                    <ul class="flex flex-col gap-2">
                        <FormBase :onSubmit="async () => addCustomCondition()">
                            <label>Add Condition</label>
                            <div class="flex items-center gap-2">
                                <FormInput
                                    class="flex-1"
                                    v-model:value="
                                        customConditionState.newConditionName
                                    "
                                />
                                <FormButton
                                    icon="plus"
                                    buttonColour="take-purple"
                                    type="submit"
                                />
                            </div>
                            <label
                                v-if="customConditionState.error"
                                class="text-take-red"
                                >{{ customConditionState.error }}</label
                            >
                        </FormBase>
                        <label>Current Conditions</label>
                        <li
                            v-for="(
                                customCondition, index
                            ) in filteredConditions"
                            :key="index"
                            class="flex items-center gap-2"
                        >
                            <label class="flex-1">{{
                                customCondition.name
                            }}</label>
                            <FormButton
                                icon="trash"
                                buttonColour="take-purple"
                                type="button"
                                @clicked="
                                    () =>
                                        removeCustomCondition(
                                            customCondition.id,
                                        )
                                "
                            />
                        </li>
                    </ul>
                </template>
            </Collapsable>
        </ul>
    </main>
</template>
<script setup lang="ts">
import {
    faClockRotateLeft,
    faPaintBrush,
} from "@fortawesome/free-solid-svg-icons";
import type { Condition } from "base/utils/types/models";
import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
import Collapsable from "../Collapsable.vue";
import { DefaultConditions } from "base/utils/Conditions";

const props = defineProps<{
    conditions: Condition[] | undefined;
}>();

const emit = defineEmits<{
    (e: "update:conditions", conditions: Condition[]): void;
}>();

const defaultConditionsEnabled = computed(() => {
    const outputDict: Record<string, boolean> = {};
    DefaultConditions.forEach(
        (knownCondition) =>
            (outputDict[knownCondition.name] =
                (props.conditions ?? []).find(
                    (x) => x.name == knownCondition.name,
                ) != null),
    );
    return outputDict;
});

const filteredConditions = computed(() =>
    (props.conditions ?? []).filter(
        (x) => !DefaultConditions.map((x) => x.name).includes(x.name),
    ),
);

function defaultConditionToggled(conditionName: string) {
    // Remove the condition if it is in the list.
    if (defaultConditionsEnabled.value[conditionName]) {
        const indexToRemove = (props.conditions ?? []).findIndex(
            (x) => x.name == conditionName,
        );
        const newConditionList = (props.conditions ?? []).toSpliced(
            indexToRemove,
            1,
        );
        emit("update:conditions", newConditionList);
        return;
    }

    // Add the condition otherwise.
    emit("update:conditions", [
        ...(props.conditions ?? []),
        { id: crypto.randomUUID(), name: conditionName, note: "" },
    ]);
}

const customConditionState = reactive<{
    newConditionName: string | undefined;
    error: string | undefined;
}>({
    newConditionName: undefined,
    error: undefined,
});
function addCustomCondition() {
    customConditionState.error = undefined;
    if (customConditionState.newConditionName == null) {
        customConditionState.error = "Please provide a condition name";
        return;
    }

    // Check if there is already a condition with the given name
    if (
        (props.conditions ?? [])
            .map((x) => x.name)
            .includes(customConditionState.newConditionName)
    ) {
        customConditionState.error =
            "There is already a condition with that name";
        return;
    }

    // Check if there is a default condition with the provided name.
    if (
        DefaultConditions.map((x) => x.name).includes(
            customConditionState.newConditionName,
        )
    ) {
        customConditionState.error = `Please use the provided default condition for ${customConditionState.newConditionName}`;
        return;
    }

    // Add the condition.
    emit("update:conditions", [
        ...(props.conditions ?? []),
        {
            id: crypto.randomUUID(),
            name: customConditionState.newConditionName,
            note: "",
        },
    ]);
    customConditionState.newConditionName = undefined;
}

function removeCustomCondition(id: string) {
    const index = (props.conditions ?? []).findIndex((x) => x.id == id);
    if (index == -1) {
        return;
    }

    emit("update:conditions", (props.conditions ?? []).toSpliced(index, 1));
}
</script>
