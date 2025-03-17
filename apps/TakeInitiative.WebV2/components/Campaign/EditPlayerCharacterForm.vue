<template>
    <div>
        <AutoForm
            :schema="schema"
            :form="form"
            :fieldConfig="{
                health: {
                    '!': {
                        component: INPUT_COMPONENTS.select,
                    },
                },
            }">
        </AutoForm>
        <!-- <FormBase
            class="flex flex-col gap-2"
            :onSubmit="onSubmit"
            v-slot="{ submitting }">
            <FormInput
                :autoFocus="true"
                textColour="white"
                label="Name"
                v-model:value="name"
                v-bind="nameInputProps" />
            <CampaignCharacterUnevaluatedInitiativeInput
                ref="characterInitiativeInput"
                :initiative="initiative as UnevaluatedCharacterInitiative"
                :errorMessage="initiativeProps.errorMessage" />
            <CampaignCharacterHealthInput
                ref="characterHealthInput"
                :health="{
                    value: health as UnevaluatedCharacterHealth,
                    isUnevaluated: true,
                }" />
            <CampaignCharacterArmourClassInput v-model:value="armourClass" />
            <div class="flex w-full justify-end" v-if="!props.npc">
                <FormButton
                    label="Create"
                    :loadingDisplay="{
                        showSpinner: true,
                        loadingText: 'Creating...',
                    }"
                    :isLoading="submitting && submitting.submitterName == 'Create'"
                    buttonColour="take-yellow-dark" />
            </div>
            <div v-else class="flex justify-between gap-2">
                <FormButton
                    label="Save"
                    :loadingDisplay="{
                        showSpinner: true,
                        loadingText: 'Saving...',
                    }"
                    :isLoading="submitting && submitting.submitterName == 'Save'"
                    buttonColour="take-yellow-dark" />
                <FormButton
                    icon="trash"
                    :isLoading="submitting && submitting.submitterName == 'trash'"
                    buttonColour="take-navy-light"
                    hoverButtonColour="take-red" />
            </div>
        </FormBase> -->
    </div>
</template>

<script setup lang="ts">
    import { useForm } from "vee-validate";
    import {
        unevaluatedCharacterInitiativeValidator,
        type PlayerCharacter,
        unevaluatedCharacterHealthValidator,
        type UnevaluatedCharacterInitiative,
        type UnevaluatedCharacterHealth,
    } from "~/utils/types/models";
    import type { CreatePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/createPlannedCombatNpcRequest";
    import type { PlayerCharacterDto } from "~/utils/api/campaign/createPlayerCharacterRequest";

    import { toTypedSchema } from "@vee-validate/zod";
    import { z } from "zod";
    import CharacterHealthInput from "~/components/Campaign/CharacterHealthInput.vue";

    import { INPUT_COMPONENTS } from "../ui/auto-form/constant";

    const formState = reactive({
        error: null as ApiError<CreatePlannedCombatNpcRequest> | null,
    });

    const props = defineProps<{
        character: PlayerCharacter;
        onEdit: (request: PlayerCharacterDto) => Promise<unknown>;
        onDelete: () => Promise<unknown>;
    }>();

    // Form Definition
    const schema = z
        .object({
            name: z
                .string({ required_error: "Please provide a name" })
                .min(1, "Please provide a name"),
            initiative: unevaluatedCharacterInitiativeValidator,
            armourClass: z.number().optional().nullable(),
            health: unevaluatedCharacterHealthValidator,
        })
        .required({ name: true, health: true });

    const form = useForm({
        validationSchema: toTypedSchema(schema),
        initialValues: {
            initiative: props.character.initiative,
            name: props.character.name,
            armourClass: props.character.armourClass,
            health: props.character.health,
        },
        keepValuesOnUnmount: true,
    });

    async function onDelete() {
        if (!props.onDelete) return;
        return await props.onDelete().catch((err) => {
            formState.error = parseAsApiError(err);
        });
    }

    // async function onEdit(formValues: z.infer<typeof schema>) {
    //     if (!props.onEdit) return;

    //     formState.error = null;

    //     // Fetch & Set the computed health values from the health component upon submission
    //     const computedHealth = characterHealthInput.value?.getHealth();
    //     if (computedHealth == false) {
    //         return;
    //     }
    //     health.value = computedHealth;

    //     const computedInitiative =
    //         characterInitiativeInput.value?.getInitiative();
    //     if (computedInitiative == false) {
    //         return;
    //     }
    //     initiative.value = computedInitiative;

    //     const validateResult = await validate();
    //     if (!validateResult.valid) {
    //         return;
    //     }

    //     return await props
    //         .onEdit({
    //             health: computedHealth!,
    //             initiative: computedInitiative!,
    //             name: name.value!,
    //             armourClass: armourClass.value ?? null,
    //         })
    //         .catch((error) => {
    //             formState.error = parseAsApiError(error);
    //         });
    // }

    // async function onCreate() {
    //     if (!props.onCreate) return;

    //     formState.error = null;

    //     // Fetch & Set the computed health values from the health component upon submission
    //     const computedHealth = characterHealthInput.value?.getHealth();
    //     if (computedHealth == false) {
    //         return;
    //     }
    //     health.value = computedHealth;

    //     const computedInitiative =
    //         characterInitiativeInput.value?.getInitiative();
    //     if (computedInitiative == false) {
    //         return;
    //     }

    //     initiative.value = computedInitiative;
    //     const validateResult = await validate();
    //     if (!validateResult.valid) {
    //         return;
    //     }

    //     return await props
    //         .onCreate({
    //             health: computedHealth!,
    //             initiative: computedInitiative!,
    //             name: name.value!,
    //             armourClass: armourClass.value ?? null,
    //         })
    //         .catch((error) => {
    //             formState.error = parseAsApiError(error);
    //         });
    // }
</script>
