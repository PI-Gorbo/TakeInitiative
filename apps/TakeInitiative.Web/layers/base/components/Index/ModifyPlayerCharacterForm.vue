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

        <CharacterUnevaluatedInitiativeInput
            ref="characterInitiativeInput"
            :initiative="initiative"
            :errorMessage="initiativeProps.errorMessage"
        />

        <CharacterHealthInput
            ref="characterHealthInput"
            :health="{ value: health, isUnevaluated: true }"
        />

        <CharacterArmourClassInput v-model:value="armourClass" />

        <div class="flex w-full justify-end" v-if="!props.npc">
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
    type PlayerCharacter,
    unevaluatedCharacterHealthValidator,
} from "base/utils/types/models";
import type { CreatePlannedCombatNpcRequest } from "base/utils/api/plannedCombat/stages/npcs/createPlannedCombatNpcRequest";
import type { PlayerCharacterDto } from "base/utils/api/campaign/createPlayerCharacterRequest";
import type { SubmittingState } from "../Form/Base.vue";
import { toTypedSchema } from "@vee-validate/zod";
import { z } from "zod";
import HealthInput from "../Character/HealthInput.vue";
import Initiative from "../Character/UnevaluatedInitiativeInput.vue";
const characterHealthInput = ref<InstanceType<typeof HealthInput> | null>(null);
const characterInitiativeInput = ref<InstanceType<typeof Initiative> | null>(
    null,
);

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
        z
            .object({
                name: z
                    .string({ required_error: "Please provide a name" })
                    .min(1, "Please provide a name"),
                initiative: unevaluatedCharacterInitiativeValidator,
                armourClass: z.number().nullable(),
                health: unevaluatedCharacterHealthValidator,
            })
            .required({ name: true, health: true }),
    ),
    initialValues: {
        initiative: props.npc?.initiative ?? { roll: undefined },
        name: props.npc?.name ?? "",
        armourClass: props.npc?.armourClass ?? null,
        health: props.npc?.health ?? {
            "!": "None",
        },
    },
});

onMounted(() => {
    if (props.npc) {
        initiative.value = props.npc?.initiative ?? { roll: undefined };
        name.value = props.npc?.name ?? "";
        armourClass.value = props.npc?.armourClass ?? null;
        health.value = props.npc?.health ?? {
            "!": "Fixed",
            currentHealth: undefined,
            maxHealth: undefined,
        };
    }
});

const [name, nameInputProps] = defineField("name", {
    props: (state) => ({
        errorMessage: formState.error?.getErrorFor("name") ?? state.errors[0],
    }),
});

const [initiative, initiativeProps] = defineField("initiative", {
    props: (state) => ({
        errorMessage:
            formState.error?.getErrorFor("playerCharacter.Initiative") ??
            state.errors[0],
    }),
});

const [health, healthProps] = defineField("health", {
    props: (state) => ({
        errorMessage: formState.error?.getErrorFor("health") ?? state.errors[0],
    }),
});

const [armourClass, armourClassInputProps] = defineField("armourClass", {
    props: (state) => ({
        errorMessage:
            formState.error?.getErrorFor("playerCharacter.armourClass") ??
            state.errors[0],
    }),
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
    return await props.onDelete().catch((err) => {
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
            health: computedHealth!,
            initiative: computedInitiative!,
            name: name.value!,
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
            health: computedHealth!,
            initiative: computedInitiative!,
            name: name.value!,
            armourClass: armourClass.value ?? null,
        })
        .catch((error) => {
            formState.error = parseAsApiError(error);
        });
}
</script>
