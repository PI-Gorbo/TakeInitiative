<template>
    <form @submit.prevent="onSubmit" class="flex flex-col gap-2">
        <FormFieldWrapper label="Name" :error="form.errors.value.name">
            <Input autofocus v-model="name" />
        </FormFieldWrapper>

        <CampaignCharacterInitiativeRollInput
            v-model:initiative="initiativeRoll"
            :error="form.errors.value['initiative.roll']" />

        <!-- @vue-ignore -->
        <CampaignCharacterUnevaluatedHealthInput
            v-model:health="health"
            @evaluateExpression="evaluteFromHealth"
            :error="form.errors.value.health" />

        <FormFieldWrapper
            label="Armor Class"
            :error="form.errors.value.armourClass">
            <div class="flex gap-2">
                <Input
                    type="number"
                    placeholder="Optional"
                    :modelValue="ac ?? undefined"
                    @update:modelValue="
                        (v) => {
                            if (v === '') {
                                ac = null;
                            } else {
                                //@ts-ignore
                                ac = v;
                            }
                        }
                    " />
                <Button
                    size="icon"
                    type="button"
                    variant="outline"
                    @click="() => form.setFieldValue('armourClass', null)">
                    <FontAwesomeIcon :icon="faArrowRotateLeft" />
                </Button>
            </div>
        </FormFieldWrapper>

        <ErrorPanel v-if="formState.error?.errors?.generalErrors">
            {{ formState.error?.errors?.generalErrors.at(0) }}
        </ErrorPanel>

        <div class="flex gap-1 justify-between">
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

            <Button
                :disabled="!form.meta.value.valid || !form.meta.value.dirty">
                <FontAwesomeIcon :icon="faSave" />
                {{ form.isSubmitting.value ? "Saving..." : "Save" }}
            </Button>
        </div>
    </form>
</template>

<script setup lang="ts">
    import { useForm, useFormValues } from "vee-validate";
    import {
        unevaluatedCharacterHealthValidator,
        unevaluatedCharacterInitiativeValidator,
        type DraftCombatCharacter,
        type PlayerCharacter,
    } from "~/utils/types/models";
    import type { CreatePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/createPlannedCombatNpcRequest";
    import type { PlayerCharacterDto } from "~/utils/api/campaign/createPlayerCharacterRequest";

    import { toTypedSchema } from "@vee-validate/zod";
    import { z } from "zod";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import {
        faArrowRotateLeft,
        faQuestionCircle,
        faSave,
        faSpinner,
        faTrashCan,
    } from "@fortawesome/free-solid-svg-icons";
    import type { UpdatePlayerCharacterRequest } from "~/utils/api/campaign/updatePlayerCharacterRequest";
    import {
        evaluateHealthInput,
        mappedHealthInputValidator,
    } from "~/utils/forms/healthFormValidator";
    import { armorClassFormValidator } from "~/utils/forms/armorClassFormValidator";
    import type { UpdatePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/updatePlannedCombatNpcRequest";
    import type { DeletePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/deletePlannedCombatNpcRequest";

    const formState = reactive({
        error: null as ApiError<CreatePlannedCombatNpcRequest> | null,
    });

    const props = defineProps<{
        npc: DraftCombatCharacter;
        onEdit: (
            request: Omit<UpdatePlannedCombatNpcRequest, "combatId" | "stageId">
        ) => Promise<unknown>;
        onDelete: (
            request: Omit<DeletePlannedCombatNpcRequest, "combatId" | "stageId">
        ) => Promise<unknown>;
    }>();

    // Form Definition
    const schema = z
        .object({
            name: z
                .string({ required_error: "Please provide a name" })
                .min(1, "Please provide a name"),
            initiative: unevaluatedCharacterInitiativeValidator,
            quantity: z.number().min(1, "Quantity must be 1 or more."),
            armourClass: armorClassFormValidator,
            health: mappedHealthInputValidator,
        })
        .required({ name: true, health: true });
    const form = useForm({
        validationSchema: toTypedSchema(schema),
        initialValues: {
            initiative: props.npc?.initiative,
            quantity: props.npc?.quantity ?? 1,
            name: props.npc?.name,
            armourClass: props.npc?.armourClass ?? null,
            health: props.npc?.health,
        },
    });
    const [name] = form.defineField("name");
    const [quantity] = form.defineField("quantity");
    const [initiativeRoll] = form.defineField("initiative.roll");
    const [health] = form.defineField("health");
    const [ac] = form.defineField("armourClass");

    function evaluteFromHealth() {
        //@ts-ignore
        const evalutedHealth = evaluateHealthInput(form.values.health);
        form.setValues({
            health: evalutedHealth,
        });
    }

    const onSubmit = form.handleSubmit(async (formValue, ctx) => {
        form.setValues({ health: formValue.health });

        return await props
            .onEdit({
                ...formValue,
                npcId: props.npc.id,
            })
            .catch((error) => {
                formState.error = parseAsApiError<{
                    playerCharacter: UpdatePlayerCharacterRequest["playerCharacter"];
                }>(error);

                form.setFieldError(
                    "initiative.roll",
                    //@ts-ignore
                    formState.error.errors?.[
                        "playerCharacter.Initiative.Roll"
                    ].at(0)
                );
            });
    });

    const { refresh, status } = useAsyncData(
        "delete-player-character",
        () => props.onDelete({ npcId: props.npc.id }),
        {
            immediate: false,
        }
    );
</script>initiativeRoll
