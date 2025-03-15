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

        <CharacterUnevaluatedInitiativeInput
            ref="characterInitiativeInput"
            :initiative="initiative as UnevaluatedCharacterInitiative"
            :errorMessage="initiativeInputProps.errorMessage"
        />

        <CharacterHealthInput
            :health="{
                value: health as UnevaluatedCharacterHealth,
                isUnevaluated: true,
            }"
            ref="characterHealthInput"
        />

        <CharacterArmourClassInput v-model:value="armourClass" />

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
import { useForm } from "vee-validate";
import {
    unevaluatedCharacterInitiativeValidator,
    unevaluatedCharacterHealthValidator,
    type StagedCharacter,
    type CharacterInitiative,
    type CharacterHealth,
    type UnevaluatedCharacterHealth,
    type UnevaluatedCharacterInitiative,
} from "base/utils/types/models";
import type { StagedCharacterDTO } from "base/utils/api/combat/putUpsertStagedCharacter";
import type { SubmittingState } from "../Form/Base.vue";
import type { DeleteStagedCharacterRequest } from "base/utils/api/combat/deleteStagedCharacterRequest";
import { toTypedSchema } from "@vee-validate/zod";
import { z } from "zod";
import HealthInput from "../Character/HealthInput.vue";
import type { StagedCharacterWithoutIdDTO } from "base/utils/api/combat/postAddStagedCharacter";
import type Initiative from "../Character/UnevaluatedInitiativeInput.vue";
const characterHealthInput = ref<InstanceType<typeof HealthInput> | null>(null);
const characterInitiativeInput = ref<InstanceType<typeof Initiative> | null>(
    null,
);
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
            .required(),
    ),
    initialValues: {
        initiative: props.character?.initiative,
        name: props.character?.name,
        isHidden: props.character?.hidden ?? false,
        armourClass: props.character?.armourClass ?? null,
        health: props.character?.health,
    },
});
const [name, nameInputProps] = defineField("name", {
    props: (state) => ({
        errorMessage: formState.error?.getErrorFor("name") ?? state.errors[0],
    }),
});

const [initiative, initiativeInputProps] = defineField("initiative", {
    props: (state) => ({
        errorMessage:
            formState.error?.getErrorFor("character.Initiative.Value") ??
            state.errors[0],
    }),
});

const [isHidden, isHiddenInputProps] = defineField("isHidden", {
    props: (state) => ({
        errorMessage:
            formState.error?.getErrorFor("character.Hidden") ?? state.errors[0],
    }),
});

const [health, healthInputProps] = defineField("health", {
    props: (state) => ({
        errorMessage:
            (formState.error?.getErrorFor("playerCharacter.Health.HasHealth") ||
                formState.error?.getErrorFor("in")) ??
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
        initiative.value = props.character?.initiative;
        name.value = props.character?.name;
        isHidden.value = props.character?.hidden;
        armourClass.value = props.character?.armourClass ?? null;
        health.value = props.character?.health;
    },
    { deep: true },
);

onMounted(() => {
    if (props.character) {
        initiative.value = props.character?.initiative;
        name.value = props.character?.name;
        isHidden.value = props.character?.hidden;
        armourClass.value = props.character?.armourClass ?? null;
        health.value = props.character?.health;
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
    const computedHealth = characterHealthInput.value?.getHealth();
    if (computedHealth == false) {
        return;
    }
    health.value = computedHealth;

    const computedInitiative = characterInitiativeInput.value?.getInitiative();
    if (computedInitiative == false) {
        return;
    }
    initiative.value = computedInitiative;

    const validateResult = await validate();
    if (!validateResult.valid) {
        return;
    }

    return await props
        .onEdit({
            initiative: computedInitiative!,
            name: name.value!,
            id: props.character?.id!,
            hidden: !userIsDm.value ? false : isHidden.value!,
            health: computedHealth!,
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
    const computedHealth = characterHealthInput.value?.getHealth();
    if (computedHealth == false) {
        return;
    }
    health.value = computedHealth;

    const computedInitiative = characterInitiativeInput.value?.getInitiative();
    if (computedInitiative == false) {
        return;
    }
    initiative.value = computedInitiative;

    const validateResult = await validate();
    if (!validateResult.valid) {
        return;
    }

    return await props
        .onCreate({
            initiative: computedInitiative!,
            name: name.value!,
            hidden: !userIsDm.value ? false : isHidden.value!,
            health: computedHealth!,
            armourClass: armourClass.value ?? null,
        })
        .catch((error) => {
            formState.error = parseAsApiError(error);
        });
}
</script>
