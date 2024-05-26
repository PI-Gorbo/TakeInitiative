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
    type PlayerCharacter,
} from "~/utils/types/models";
import type { CreatePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/createPlannedCombatNpcRequest";
import type { UpdatePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/updatePlannedCombatNpcRequest";
import type { DeletePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/deletePlannedCombatNpcRequest";
import type { PlayerCharacterDto } from "~/utils/api/campaign/createPlayerCharacterRequest";
import type { SubmittingState } from "../Form/Base.vue";

const formState = reactive({
    error: null as ApiError<CreatePlannedCombatNpcRequest> | null,
});

const props = defineProps<{
    npc?: PlayerCharacter;
    onCreate?: (request: PlayerCharacterDto) => Promise<unknown>;
    onEdit?: (request: PlayerCharacterDto) => Promise<unknown>;
    onDelete?: () => Promise<unknown>;
}>();

// Form Definition
const { values, errors, defineField, validate } = useForm({
    validationSchema: toTypedSchema(
        yup.object({
            name: yup.string().required("Please provide a name"),
            initiative: characterInitiativeValidator,
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

onMounted(() => {
    if (!props.npc) {
        initiativeStrategy.value = InitiativeStrategy.Roll;
    } else {
        initiativeStrategy.value = props.npc?.initiative.strategy;
        initiativeValue.value = props.npc.initiative.value;
        name.value = props.npc.name;
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
    return await props.onDelete().catch(async (err) => {
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
        })
        .catch(async (error) => {
            formState.error = await parseAsApiError(error);
        });
}
</script>
