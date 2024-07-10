<template>
    <FormBase
        class="flex flex-col gap-2"
        :onSubmit="onSubmit"
        v-slot="{ submitting }"
    >
        <FormInput
            :autoFocus="true"
            textColour="white"
            label="Name"
            v-model:value="name"
            v-bind="nameInputProps"
        />
        <FormInput
            label="Quantity"
            textColour="white"
            type="number"
            :value="quantity"
            @update:value="(val) => (quantity = Number(val) ?? 1)"
            v-bind="quantityInputProps"
        />

        <CharacterInitiative
            v-model:initiativeStrategy="initiativeStrategy"
            v-model:initiativeValue="initiativeValue"
            :errorMessage="
                initiativeStrategyInputProps.errorMessage ||
                initiativeValueInputProps.errorMessage
            "
        />

        <CharacterHealthInput
            v-model:hasHealth="hasHealth"
            v-model:currentHealth="currentHealth"
            v-model:maxHealth="maxHealth"
            :error="
                hasHealthInputProps.errorMessage ??
                currentHealthInputProps.errorMessage ??
                maxHealthInputProps.errorMessage
            "
        />

        <CharacterArmourClass v-model:value="armourClass" />

        <div class="flex w-full justify-center" v-if="!props.npc">
            <FormButton
                label="Create"
                :loadingDisplay="{
                    showSpinner: true,
                    loadingText: 'Creating...',
                }"
                :isLoading="submitting && submitting.submitterName == 'Create'"
                buttonColour="take-yellow-dark"
            />
        </div>
        <div v-else class="flex justify-between gap-2">
            <FormButton
                label="Edit"
                :loadingDisplay="{
                    showSpinner: true,
                    loadingText: 'Saving...',
                }"
                :isLoading="submitting && submitting.submitterName == 'Save'"
                buttonColour="take-yellow-dark"
            />
            <FormButton
                icon="trash"
                :isLoading="submitting && submitting.submitterName == 'trash'"
                buttonColour="take-navy-light"
                hoverButtonColour="take-red"
            />
        </div>
    </FormBase>
</template>

<script setup lang="ts">
import { Form } from "vee-validate";
import { toTypedSchema } from "@vee-validate/yup";
import { useForm } from "vee-validate";
import * as yup from "yup";
import {
    InitiativeStrategy,
    plannedCombatCharacterValidator,
    type PlannedCombatCharacter,
    type PlannedCombatStage,
    characterInitiativeValidator,
    characterHealthValidator,
} from "base/utils/types/models";
import type { CreatePlannedCombatNpcRequest } from "base/utils/api/plannedCombat/stages/npcs/createPlannedCombatNpcRequest";
import type { UpdatePlannedCombatNpcRequest } from "base/utils/api/plannedCombat/stages/npcs/updatePlannedCombatNpcRequest";
import type { DeletePlannedCombatNpcRequest } from "base/utils/api/plannedCombat/stages/npcs/deletePlannedCombatNpcRequest";
import type { SubmittingState } from "../Form/Base.vue";

const formState = reactive({
    error: null as ApiError<CreatePlannedCombatNpcRequest> | null,
});

const props = defineProps<{
    npc?: PlannedCombatCharacter;
    onCreate?: (
        request: Omit<CreatePlannedCombatNpcRequest, "combatId" | "stageId">,
    ) => Promise<unknown>;
    onEdit?: (
        request: Omit<UpdatePlannedCombatNpcRequest, "combatId" | "stageId">,
    ) => Promise<unknown>;
    onDelete?: (
        request: Omit<DeletePlannedCombatNpcRequest, "combatId" | "stageId">,
    ) => Promise<unknown>;
}>();

// Form Definition
const { values, errors, defineField, validate } = useForm({
    validationSchema: toTypedSchema(
        yup.object({
            name: yup.string().required("Please provide a name"),
            initiative: characterInitiativeValidator,
            quantity: yup.number().min(1, "Quantity must be 1 or more."),
            armourClass: yup.number().nullable(),
            health: characterHealthValidator.required(),
        }),
    ),
});
const [name, nameInputProps] = defineField("name", {
    props: (state) => ({
        errorMessage: formState.error?.getErrorFor("name") ?? state.errors[0],
    }),
});

const [quantity, quantityInputProps] = defineField("quantity", {
    props: (state) => ({
        errorMessage:
            formState.error?.getErrorFor("quantity") ?? state.errors[0],
    }),
});

const [initiativeStrategy, initiativeStrategyInputProps] = defineField(
    "initiative.strategy",
    {
        props: (state) => ({
            errorMessage:
                formState.error?.getErrorFor("initiative.strategy") ??
                state.errors[0],
        }),
    },
);

const [initiativeValue, initiativeValueInputProps] = defineField(
    "initiative.value",
    {
        props: (state) => ({
            errorMessage:
                formState.error?.getErrorFor("initiative.Value") ??
                state.errors[0],
        }),
    },
);

const [hasHealth, hasHealthInputProps] = defineField("health.hasHealth", {
    props: (state) => ({
        errorMessage:
            (formState.error?.getErrorFor("playerCharacter.Health.HasHealth") ||
                formState.error?.getErrorFor("in")) ??
            state.errors[0],
    }),
});

const [currentHealth, currentHealthInputProps] = defineField(
    "health.currentHealth",
    {
        props: (state) => ({
            errorMessage:
                formState.error?.getErrorFor(
                    "playerCharacter.Health.CurrentHealth",
                ) ?? state.errors[0],
        }),
    },
);

const [maxHealth, maxHealthInputProps] = defineField("health.maxHealth", {
    props: (state) => ({
        errorMessage:
            formState.error?.getErrorFor("playerCharacter.Health.MaxHealth") ??
            state.errors[0],
    }),
});

const [armourClass, armourClassInputProps] = defineField("armourClass", {
    props: (state) => ({
        errorMessage:
            formState.error?.getErrorFor("playerCharacter.armourClass") ??
            state.errors[0],
    }),
});

onMounted(() => {
    if (!props.npc) {
        initiativeStrategy.value = InitiativeStrategy.Roll;
        quantity.value = 1;
        armourClass.value = null;
        hasHealth.value = false;
        currentHealth.value = 0;
        maxHealth.value = 0;
    } else {
        initiativeStrategy.value = props.npc?.initiative.strategy;
        initiativeValue.value = props.npc.initiative.value;
        quantity.value = props.npc.quantity;
        name.value = props.npc.name;
        armourClass.value = props.npc.armourClass ?? null;
        hasHealth.value = props.npc.health?.hasHealth ?? false;
        currentHealth.value = props.npc.health?.currentHealth;
        maxHealth.value = props.npc.health?.maxHealth;
    }
});

async function onSubmit(formSubmittingState: SubmittingState) {
    if (formSubmittingState.submitterName == "Create") {
        await onCreate();
    }

    if (formSubmittingState.submitterName == "trash") {
        await onDelete();
    }

    if (formSubmittingState.submitterName == "Save") {
        await onEdit();
    }
}

async function onDelete() {
    if (!props.onDelete) return;
    return await props
        .onDelete({ npcId: props.npc?.id! })
        .catch(async (err) => {
            formState.error = await parseAsApiError(err);
        });
}

async function onEdit() {
    if (!props.onEdit) return;

    formState.error = null;
    const validateResult = await validate();
    if (!validateResult.valid) {
        return;
    }

    return await props
        .onEdit({
            health: {
                hasHealth: hasHealth.value ?? false,
                currentHealth: currentHealth.value,
                maxHealth: maxHealth.value,
            },
            initiative: {
                strategy: initiativeStrategy.value!,
                value: initiativeValue.value!,
            },
            name: name.value!,
            quantity: quantity.value!,
            armourClass: armourClass.value ?? null,
            npcId: props.npc?.id!,
        })
        .catch(async (error) => {
            formState.error = await parseAsApiError(error);
        });
}

async function onCreate() {
    if (!props.onCreate) return;

    formState.error = null;
    const validateResult = await validate();
    if (!validateResult.valid) {
        return;
    }
    return await props
        .onCreate({
            health: {
                hasHealth: hasHealth.value ?? false,
                currentHealth: currentHealth.value,
                maxHealth: maxHealth.value,
            },
            initiative: {
                strategy: initiativeStrategy.value!,
                value: initiativeValue.value!,
            },
            name: name.value!,
            quantity: quantity.value!,
            armourClass: armourClass.value ?? null,
        })
        .catch(async (error) => {
            formState.error = await parseAsApiError(error);
        });
}
</script>
