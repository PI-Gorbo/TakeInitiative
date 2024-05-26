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
                formState.error?.getErrorFor("initiative.value") ??
                state.errors[0],
        }),
    },
);

const [isHidden, isHiddenInputProps] = defineField("isHidden", {
    props: (state) => ({
        errorMessage: formState.error?.getErrorFor("hidden") ?? state.errors[0],
    }),
});

watch(
    () => props.character,
    () => {
        initiativeStrategy.value = props.character?.initiative.strategy;
        initiativeValue.value = props.character?.initiative.value;
        name.value = props.character?.name;
        isHidden.value = props.character?.hidden;
    },
    { deep: true },
);

onMounted(() => {
    if (!props.character) {
        initiativeStrategy.value = InitiativeStrategy.Roll;
        isHidden.value = userIsDm.value;
    } else {
        initiativeStrategy.value = props.character?.initiative.strategy;
        initiativeValue.value = props.character.initiative.value;
        name.value = props.character.name;
        isHidden.value = props.character?.hidden;
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
            health: null,
            initiative: {
                strategy: initiativeStrategy.value!,
                value: initiativeValue.value!,
            },
            name: name.value!,
            armorClass: null,
            id: props.character?.id!,
            hidden: isHidden.value!,
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
            health: null,
            initiative: {
                strategy: initiativeStrategy.value!,
                value: initiativeValue.value!,
            },
            name: name.value!,
            armorClass: null,
            id: props.character?.id! ?? crypto.randomUUID(),
            hidden: isHidden.value!,
        })
        .catch(async (error) => {
            formState.error = await parseAsApiError(error);
        });
}
</script>
