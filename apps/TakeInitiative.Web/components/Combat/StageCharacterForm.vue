<template>
    <FormBase
        class="flex flex-col gap-2 px-1"
        :onSubmit="onSubmit"
        v-slot="{ submitting }"
    >
        <div class="flex items-end justify-between">
            <FormInput
                :autoFocus="true"
                textColour="white"
                label="Name"
                v-model:value="name"
                v-bind="nameInputProps"
            />

            <FormButton
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
            <div class="flex flex-row">
                <select
                    name="Initiative Strategy"
                    :value="initiativeStrategy"
                    @input="
                        (e: Event) =>
                            (initiativeStrategy = Number(
                                (e.target as HTMLSelectElement).value,
                            ))
                    "
                    class="rounded-l-lg bg-take-grey-dark py-1 pl-2 pr-1"
                >
                    <option :value="InitiativeStrategy.Fixed">Fixed</option>
                    <option :value="InitiativeStrategy.Roll">Roll</option>
                </select>

                <input
                    type="text"
                    class="flex-1 rounded-r-lg bg-take-navy-light px-1 text-white"
                    :value="initiativeValue"
                    @input="
                        (e) =>
                            (initiativeValue = (e.target as HTMLInputElement)
                                .value)
                    "
                    :placeholder="
                        initiativeStrategy == InitiativeStrategy.Fixed
                            ? '+5'
                            : '1d20 + 5'
                    "
                />
            </div>
            <label
                v-if="initiativeStrategyInputProps.errorMessage"
                class="text-take-red"
                >{{ initiativeStrategyInputProps.errorMessage }}</label
            >
            <label
                v-if="initiativeValueInputProps.errorMessage != null"
                class="text-take-red"
            >
                {{ initiativeValueInputProps.errorMessage }}
            </label>
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
        initiativeValue.value = "1d20 + 1";
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
            hidden: false,
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
            hidden: false,
        })
        .catch(async (error) => {
            formState.error = await parseAsApiError(error);
        });
}
</script>
