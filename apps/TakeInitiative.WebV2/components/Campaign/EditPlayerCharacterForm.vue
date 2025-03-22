<template>
    <form @submit.prevent="onSubmit" class="flex flex-col gap-2">
        <FormFieldWrapper label="Name" :error="form.errors.value.name">
            <Input autofocus v-model="name" />
        </FormFieldWrapper>

        <FormFieldWrapper
            label="Initiative"
            :error="form.errors.value['initiative.roll']">
            <div class="flex gap-2">
                <Input v-model="initiative" />
                <TooltipWrapper>
                    <template #Trigger>
                        <FontAwesomeIcon :icon="faQuestionCircle" />
                    </template>
                    <template #Content>
                        <div class="flex flex-col">
                            <span
                                >Initiative is rolled when the character enters
                                combat.
                            </span>
                            <span
                                >The higher a character's initiative, the
                                earlier they act in a round of combat. Supports
                                fixed number, or a dice roll.</span
                            >
                            <span
                                >An example of a roll is '1d20 + 2d4 + 3' which
                                sums one 20 sided dice, two 4 sided die and adds
                                3.</span
                            >
                        </div>
                    </template>
                </TooltipWrapper>
            </div>
        </FormFieldWrapper>

        {{ formState.error }}

        <div class="flex gap-1 justify-end">
            <div class="flex flex-1 items-center text-sm">
                <Button
                    :disabled="
                        !form.meta.value.valid || !form.meta.value.dirty
                    ">
                    <FontAwesomeIcon :icon="faSave" />
                    {{ form.isSubmitting.value ? "Saving..." : "Save" }}
                </Button>
            </div>

            <Button
                type="button"
                size="icon"
                variant="destructive"
                @click="() => refresh()">
                <FontAwesomeIcon
                    :icon="
                        status === 'success' || status === 'idle'
                            ? faTrashCan
                            : faSpinner
                    "
                    :class="{
                        'fa-spin': status === 'pending' || status === 'error',
                    }" />
            </Button>
        </div>

        <!-- <div>
        <FormBase
            class="flex flex-col gap-2"
            :onSubmit="onSubmit"
            v-slot="{ submitting }">
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
                    :isLoading="
                        submitting && submitting.submitterName == 'Create'
                    "
                    buttonColour="take-yellow-dark" />
            </div>
            <div v-else class="flex justify-between gap-2">
                <FormButton
                    label="Save"
                    :loadingDisplay="{
                        showSpinner: true,
                        loadingText: 'Saving...',
                    }"
                    :isLoading="
                        submitting && submitting.submitterName == 'Save'
                    "
                    buttonColour="take-yellow-dark" />
                <FormButton
                    icon="trash"
                    :isLoading="
                        submitting && submitting.submitterName == 'trash'
                    "
                    buttonColour="take-navy-light"
                    hoverButtonColour="take-red" />
            </div>
        </FormBase>
    </div> -->
    </form>
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
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import {
        faArrowCircleRight,
        faArrowRotateRight,
        faCheckCircle,
        faQuestionCircle,
        faSave,
        faSpinner,
        faTrashCan,
    } from "@fortawesome/free-solid-svg-icons";
    import type { UpdatePlayerCharacterRequest } from "~/utils/api/campaign/updatePlayerCharacterRequest";

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
    const [name] = form.defineField("name");
    const [initiative] = form.defineField("initiative.roll");

    const onSubmit = form.handleSubmit(async (formValue, ctx) => {
        // Fetch & Set the computed health values from the health component upon submission
        // const computedHealth = characterHealthInput.value?.getHealth();
        // if (computedHealth == false) {
        //     return;
        // }
        // health.value = computedHealth;

        // const computedInitiative =
        //     characterInitiativeInput.value?.getInitiative();
        // if (computedInitiative == false) {
        //     return;
        // }
        // initiative.value = computedInitiative;

        // const validateResult = await validate();
        // if (!validateResult.valid) {
        //     return;
        // }

        return await props
            .onEdit({
                health: formValue.health,
                initiative: formValue.initiative,
                name: formValue.name,
                armourClass: formValue.armourClass ?? null,
            })
            .catch((error) => {
                console.log("TESTING!")
                formState.error =
                    parseAsApiError<UpdatePlayerCharacterRequest>(error);
                console.log(formState.error.errors?.["playerCharacter.Initiative.Roll" ].at(0));
            });
    });

    const { refresh, status } = useAsyncData(
        "delete-player-character",
        props.onDelete,
        {
            immediate: false,
        }
    );

    defineExpose({
        onSubmit,
    });

    // async function onDelete() {
    //     if (!props.onDelete) return;
    //     return await props.onDelete().catch((err) => {
    //         formState.error = parseAsApiError(err);
    //     });
    // }

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
