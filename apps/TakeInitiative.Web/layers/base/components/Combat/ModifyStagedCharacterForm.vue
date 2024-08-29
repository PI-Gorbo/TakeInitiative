<template>
    <FormBase
        class="flex flex-col gap-2 px-1"
        :onSubmit="onSubmit"
        v-slot="{ submitting }"
    >
        <div class="flex items-end justify-between gap-2">
            <FormInput
                class="flex-1"
                :autoFocus="true"
                textColour="white"
                label="Name"
                v-model:value="name"
                v-bind="nameInputProps"
            />

            <FormButton
                v-if="userIsDm"
                :icon="isHidden ? 'eye-slash' : 'eye'"
                size="lg"
                :label="isHidden ? 'Hidden' : 'Visible'"
                @clicked="() => (isHidden = !isHidden)"
                buttonColour="take-navy-light"
                type="button"
            />
        </div>

        <CharacterInitiative
            v-model:initiativeStrategy="initiativeStrategy"
            v-model:initiativeValue="initiativeValue"
            :errorMessage="
                initiativeStrategyInputProps.errorMessage ||
                initiativeValueInputProps.errorMessage
            "
        />

        <CharacterHealthInput
            :hasHealth="hasHealth"
            :currentHealth="currentHealth"
            :maxHealth="maxHealth"
            ref="characterHealthInput"
        />

        <CharacterArmourClass v-model:value="armourClass" />

        <div class="flex w-full justify-end" v-if="!props.character">
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
                label="Save"
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
import { ErrorMessage, Form } from "vee-validate";
import { useForm } from "vee-validate";
import {
    InitiativeStrategy,
    plannedCombatCharacterValidator,
    type PlannedCombatCharacter,
    type PlannedCombatStage,
    unevaluatedCharacterInitiativeValidator,
    type InitiativeCharacter,
    unevaluatedCharacterHealthValidator,
    type StagedCharacter,
} from "base/utils/types/models";
import type { StagedCharacterDTO } from "base/utils/api/combat/putUpsertStagedCharacter";
import type { SubmittingState } from "../Form/Base.vue";
import type { DeleteStagedCharacterRequest } from "base/utils/api/combat/deleteStagedCharacterRequest";
import { toTypedSchema } from "@vee-validate/zod";
import { z } from "zod";
import HealthInput from "../Character/HealthInput.vue";
import type { StagedCharacterWithoutIdDTO } from "base/utils/api/combat/postAddStagedCharacter";
const characterHealthInput = ref<InstanceType<typeof HealthInput> | null>(null);
const { userIsDm } = storeToRefs(useCombatStore());
const formState = reactive({
    error: null as ApiError<StagedCharacterDTO> | null,
});

const props = defineProps<{
    character?: StagedCharacter;
    onCreate?: (request: StagedCharacterWithoutIdDTO) => Promise<any>;
    onEdit?: (request: StagedCharacterDTO) => Promise<any>;
    onDelete?: (
        request: Omit<DeleteStagedCharacterRequest, "combatId">,
    ) => Promise<any>;
}>();

// Form Definition
const { values, errors, defineField, validate } = useForm({
    validationSchema: toTypedSchema(
        z
            .object({
                name: z.string({ required_error: "Please provide a name" }),
                initiative: unevaluatedCharacterInitiativeValidator,
                isHidden: z.boolean(),
                armourClass: z.number().nullable(),
                health: unevaluatedCharacterHealthValidator,
            })
            .required({ name: true, health: true }),
    ),
});
const [name, nameInputProps] = defineField("name", {
    props: (state) => ({
        errorMessage: formState.error?.getErrorFor("name") ?? state.errors[0],
    }),
});

const [initiativeStrategy, initiativeStrategyInputProps] = defineField(
    "initiative.strategy",
    {
        props: (state) => ({
            errorMessage:
                formState.error?.getErrorFor("character.Initiative.Strategy") ??
                state.errors[0],
        }),
    },
);

const [initiativeValue, initiativeValueInputProps] = defineField(
    "initiative.value",
    {
        props: (state) => ({
            errorMessage:
                formState.error?.getErrorFor("character.Initiative.Value") ??
                state.errors[0],
        }),
    },
);

const [isHidden, isHiddenInputProps] = defineField("isHidden", {
    props: (state) => ({
        errorMessage:
            formState.error?.getErrorFor("character.Hidden") ?? state.errors[0],
    }),
});

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

watch(
    () => props.character,
    () => {
        initiativeStrategy.value = props.character?.initiative.strategy;
        initiativeValue.value = props.character?.initiative.value;
        name.value = props.character?.name;
        isHidden.value = props.character?.hidden;
        armourClass.value = props.character?.armourClass ?? null;
        hasHealth.value = props.character?.health?.hasHealth ?? false;
        currentHealth.value = props.character?.health?.currentHealth;
        maxHealth.value = props.character?.health?.maxHealth;
    },
    { deep: true },
);

onMounted(() => {
    if (!props.character) {
        initiativeStrategy.value = InitiativeStrategy.Roll;
        isHidden.value = userIsDm.value;
        armourClass.value = null;
        hasHealth.value = false;
        currentHealth.value = 0;
        maxHealth.value = 0;
    } else {
        initiativeStrategy.value = props.character?.initiative.strategy;
        initiativeValue.value = props.character.initiative.value;
        name.value = props.character.name;
        isHidden.value = props.character?.hidden ?? false;
        armourClass.value = props.character.armourClass ?? null;
        hasHealth.value = props.character.health?.hasHealth ?? false;
        currentHealth.value = props.character.health?.currentHealth;
        maxHealth.value = props.character.health?.maxHealth;
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
        .onDelete({ characterId: props.character?.id! })
        .catch((err) => {
            formState.error = parseAsApiError(err);
        });
}

async function onEdit() {
    if (!props.onEdit) return;

    formState.error = null;

    // Fetch & Set the computed health values from the health component upon submission
    const health = characterHealthInput.value?.getHealth();
    if (health == false) {
        return;
    }
    hasHealth.value = health?.hasHealth;
    currentHealth.value = health?.currentHealth;
    maxHealth.value = health?.maxHealth;

    const validateResult = await validate();
    if (!validateResult.valid) {
        return;
    }

    return await props
        .onEdit({
            initiative: {
                strategy: initiativeStrategy.value!,
                value: initiativeValue.value!,
            },
            name: name.value!,
            id: props.character?.id!,
            hidden: isHidden.value!,
            health: {
                hasHealth: hasHealth.value ?? false,
                currentHealth: currentHealth.value!,
                maxHealth: maxHealth.value!,
            },
            armourClass: armourClass.value ?? null,
        })
        .catch((error) => {
            formState.error = parseAsApiError(error);
        });
}

async function onCreate() {
    if (!props.onCreate) return;

    formState.error = null;

    // Fetch & Set the computed health values from the health component upon submission
    const health = characterHealthInput.value?.getHealth();
    if (health == false) {
        return;
    }
    hasHealth.value = health?.hasHealth;
    currentHealth.value = health?.currentHealth;
    maxHealth.value = health?.maxHealth;

    const validateResult = await validate();
    if (!validateResult.valid) {
        return;
    }

    return await props
        .onCreate({
            initiative: {
                strategy: initiativeStrategy.value!,
                value: initiativeValue.value!,
            },
            name: name.value!,
            hidden: isHidden.value!,
            health: {
                hasHealth: hasHealth.value ?? false,
                currentHealth: currentHealth.value!,
                maxHealth: maxHealth.value!,
            },
            armourClass: armourClass.value ?? null,
        })
        .catch((error) => {
            formState.error = parseAsApiError(error);
        });
}
</script>
