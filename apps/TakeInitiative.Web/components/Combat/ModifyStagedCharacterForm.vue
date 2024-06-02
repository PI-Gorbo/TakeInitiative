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

        <section>
            <label class="text-white">Initiative</label>
            <CharacterInitiative
                v-model:initiativeStrategy="initiativeStrategy"
                v-model:initiativeValue="initiativeValue"
                :errorMessage="
                    initiativeStrategyInputProps.errorMessage ||
                    initiativeValueInputProps.errorMessage
                "
            />
        </section>

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

        <div class="flex w-full justify-center" v-if="!props.character">
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
import { toTypedSchema } from "@vee-validate/yup";
import { useForm } from "vee-validate";
import * as yup from "yup";
import {
    InitiativeStrategy,
    plannedCombatCharacterValidator,
    type PlannedCombatCharacter,
    type PlannedCombatStage,
    characterInitiativeValidator,
    type CombatCharacter,
    characterHealthValidator,
} from "~/utils/types/models";
import type { StagedCharacterDTO } from "~/utils/api/combat/putUpsertStagedCharacter";
import type { SubmittingState } from "../Form/Base.vue";
import type { DeleteStagedCharacterRequest } from "~/utils/api/combat/deleteStagedCharacterRequest";
const { userIsDm } = storeToRefs(useCombatStore());
const formState = reactive({
    error: null as ApiError<StagedCharacterDTO> | null,
});

const props = defineProps<{
    character?: CombatCharacter;
    onCreate?: (request: StagedCharacterDTO) => Promise<any>;
    onEdit?: (request: StagedCharacterDTO) => Promise<any>;
    onDelete?: (
        request: Omit<DeleteStagedCharacterRequest, "combatId">,
    ) => Promise<any>;
}>();

// Form Definition
const { values, errors, defineField, validate } = useForm({
    validationSchema: toTypedSchema(
        yup.object({
            name: yup.string().required("Please provide a name"),
            initiative: characterInitiativeValidator,
            quantity: yup.number().min(1),
            isHidden: yup.boolean(),
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
        currentHealth.value = props.character?.health?.currentHealth ?? 0;
        maxHealth.value = props.character?.health?.maxHealth ?? 0;
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
        isHidden.value = props.character?.hidden;
        armourClass.value = props.character.armourClass ?? null;
        hasHealth.value = props.character.health?.hasHealth ?? false;
        currentHealth.value = props.character.health?.currentHealth ?? 0;
        maxHealth.value = props.character.health?.maxHealth ?? 0;
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
            initiative: {
                strategy: initiativeStrategy.value!,
                value: initiativeValue.value!,
            },
            name: name.value!,

            id: props.character?.id!,
            hidden: isHidden.value!,
            health: {
                hasHealth: hasHealth.value ?? false,
                currentHealth: currentHealth.value ?? 0,
                maxHealth: maxHealth.value ?? 0,
            },
            armourClass: armourClass.value ?? null,
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
            initiative: {
                strategy: initiativeStrategy.value!,
                value: initiativeValue.value!,
            },
            name: name.value!,

            id: props.character?.id! ?? crypto.randomUUID(),
            hidden: isHidden.value!,
            health: {
                hasHealth: hasHealth.value ?? false,
                currentHealth: currentHealth.value ?? 0,
                maxHealth: maxHealth.value ?? 0,
            },
            armourClass: armourClass.value ?? null,
        })
        .catch(async (error) => {
            formState.error = await parseAsApiError(error);
        });
}
</script>
