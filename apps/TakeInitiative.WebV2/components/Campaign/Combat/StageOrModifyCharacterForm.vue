<template>
    <FormBase
        class="flex flex-col gap-2 px-1"
        :onSubmit="onSubmit"
        v-slot="{ submitting }">
        <div class="flex items-end justify-between gap-2">
            <FormFieldWrapper
                class="flex-1"
                label="Name"
                :error="nameInputProps.errorMessage">
                <Input
                    :autoFocus="true"
                    textColour="white"
                    label="Name"
                    v-model="name" />
            </FormFieldWrapper>

            <Button
                v-if="userIsDm && props.character?.playerId == userStore.state.user?.userId"
                @click="() => (isHidden = !isHidden)"
                type="button"
                variant="outline"
                class="interactable bg-[#1e293b]">
                <FontAwesomeIcon :icon="isHidden ? faEyeSlash : faEye" />
                {{ isHidden ? "Hidden" : "Visible" }}
            </Button>
        </div>

        <CampaignCharacterInitiativeRollInput
            v-model:initiative="initiative!.roll"
            :error="initiativeInputProps.errorMessage" />

        <CampaignCharacterHealthInput
            @update:health="(h) => (health = h)"
            :error="healthInputProps.errorMessage"
            :allowRoll="true"
            :health="health! as FormHealthInput" />

        <CampaignCharacterArmourClassInput
            v-model:ac="armourClass"
            :error="armourClassInputProps.errorMessage" />

        <div
            class="flex w-full justify-end"
            v-if="!props.character">
            <AsyncButton
                name="Create"
                label="Create"
                :loadingLabel="'Creating...'"
                :isLoading="
                    (submitting && submitting.submitterName == 'Create') ??
                    false
                "
                type="submit" />
        </div>
        <div
            v-else
            class="flex justify-between gap-2">
            <AsyncButton
                type="submit"
                name="Update"
                label="Save"
                loadingLabel="Saving..."
                :isLoading="
                    (submitting && submitting.submitterName == 'Update') ??
                    false
                " />
            <AsyncButton
                type="button"
                name="Delete"
                :icon="faTrash"
                :isLoading="
                    (submitting && submitting.submitterName == 'Delete') ??
                    false
                " />
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
    } from "~/utils/types/models";
    import type { StagedCharacterDTO } from "~/utils/api/combat/putUpsertStagedCharacter";
    import type { DeleteStagedCharacterRequest } from "~/utils/api/combat/deleteStagedCharacterRequest";
    import { toTypedSchema } from "@vee-validate/zod";
    import { z } from "zod";
    import type { StagedCharacterWithoutIdDTO } from "~/utils/api/combat/postAddStagedCharacter";
    import {
        faEye,
        faEyeSlash,
        faTrash,
    } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import { deleteCampaignRequest } from "~/utils/api/campaign/deleteCampaignRequest";
    import type { SubmittingState } from "~/components/Form/Base.vue";
    import {
        useAddStagedCharacterMutation,
        useDeleteStagedCharacterMutation,
        useEditStagedCharacterMutation,
    } from "~/utils/queries/combats";
    import { toast } from "vue-sonner";
    import {
        mappedHealthInputValidator,
        type FormHealthInput,
    } from "~/utils/forms/healthFormValidator";
    const userStore = useUserStore();
    const { userIsDm } = storeToRefs(useCombatStore());
    const formState = reactive({
        error: null as ApiError<StagedCharacterDTO> | null,
    });

    const props = defineProps<{
        combatId: string;
        character?: StagedCharacter;
    }>();

    const emits = defineEmits<{
        submitted: [];
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
                    health: mappedHealthInputValidator,
                })
                .required()
        ),
        initialValues: {
            initiative: props.character?.initiative ?? { roll: undefined },
            name: props.character?.name ?? "",
            isHidden: props.character?.hidden ?? false,
            armourClass: props.character?.armourClass ?? null,
            health: props.character?.health ?? { "!": "None" },
        },
    });
    const [name, nameInputProps] = defineField("name", {
        props: (state) => ({
            errorMessage:
                formState.error?.errors?.name?.at(0) ?? state.errors[0],
        }),
    });

    const [initiative, initiativeInputProps] = defineField("initiative", {
        props: (state) => ({
            errorMessage:
                formState.error
                    ?.getUntypedError("character.Initiative.Value")
                    ?.at(0) ?? state.errors[0],
        }),
    });

    const [isHidden, isHiddenInputProps] = defineField("isHidden", {
        props: (state) => ({
            errorMessage:
                formState.error?.getUntypedError("character.Hidden")?.at(0) ??
                state.errors[0],
        }),
    });

    const [health, healthInputProps] = defineField("health", {
        props: (state) => ({
            errorMessage:
                formState.error
                    ?.getUntypedError("playerCharacter.Health.HasHealth")
                    ?.at(0) ??
                formState.error?.getUntypedError("in")?.at(0) ??
                state.errors[0],
        }),
    });

    const [armourClass, armourClassInputProps] = defineField("armourClass", {
        props: (state) => ({
            errorMessage:
                formState.error
                    ?.getUntypedError("playerCharacter.armourClass")
                    ?.at(0) ?? state.errors[0],
        }),
    });

    watch(
        () => props.character,
        () => {
            console.log("updated character");
            initiative.value = props.character?.initiative;
            name.value = props.character?.name;
            isHidden.value = props.character?.hidden ?? true;
            armourClass.value = props.character?.armourClass ?? null;
            health.value = props.character?.health;
        },
        { deep: true }
    );

    onMounted(() => {
        if (props.character) {
            initiative.value = props.character?.initiative;
            name.value = props.character?.name;
            isHidden.value = props.character?.hidden ?? true;
            armourClass.value = props.character?.armourClass ?? null;
            health.value = props.character?.health;
        }
    });

    async function onSubmit(formSubmittingState: SubmittingState) {
        if (
            formSubmittingState.submitterName == "Create" ||
            (!props.character && !formSubmittingState.submitterName)
        ) {
            await onCreate();
        }

        if (formSubmittingState.submitterName == "Delete") {
            await onDelete();
        }

        if (
            formSubmittingState.submitterName == "Update" ||
            (props.character && !formSubmittingState.submitterName)
        ) {
            await onEdit();
        }
    }

    const deleteMutation = useDeleteStagedCharacterMutation();
    async function onDelete() {
        if (!props.character) return;
        return await deleteMutation
            .mutateAsync({
                characterId: props.character?.id!,
                combatId: props.combatId,
            })
            .catch((err) => {
                formState.error = parseAsApiError(err);
            })
            .then(() => {
                toast.success("Character deleted.");
                emits("submitted");
            });
    }

    const editMutation = useEditStagedCharacterMutation();
    async function onEdit() {
        if (!props.character) return;

        formState.error = null;

        const validateResult = await validate();
        if (!validateResult.valid) {
            return;
        }

        return await editMutation
            .mutateAsync({
                character: {
                    id: props.character?.id!,
                    initiative: validateResult.values?.initiative!,
                    name: validateResult.values?.name!,
                    hidden: !userIsDm.value
                        ? false
                        : validateResult.values?.isHidden!,
                    health: validateResult.values?.health!,
                    armourClass: validateResult.values?.armourClass ?? null,
                },
                combatId: props.combatId,
            })
            .catch((error) => {
                formState.error = parseAsApiError(error);
            })
            .then(() => {
                toast.success("Character updated.");
                emits("submitted");
            });
    }
    const addMutation = useAddStagedCharacterMutation();
    async function onCreate() {
        formState.error = null;
        const validateResult = await validate();
        if (!validateResult.valid) {
            return;
        }

        return await addMutation
            .mutateAsync({
                character: {
                    initiative: validateResult.values?.initiative!,
                    name: validateResult.values?.name!,
                    hidden: !userIsDm.value
                        ? false
                        : validateResult.values?.isHidden!,
                    health: validateResult.values?.health!,
                    armourClass: validateResult.values?.armourClass ?? null,
                },
                combatId: props.combatId,
            })
            .catch((error) => {
                formState.error = parseAsApiError(error);
            })
            .then(() => {
                toast.success("Character added.");
                emits("submitted");
            });
    }
</script>
