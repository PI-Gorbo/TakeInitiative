<template>
    <FormBase
        :onSubmit="submit"
        v-slot="{ submitting }">
        <main
            class="flex flex-col gap-2 pb-2"
            v-if="props.character">
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
                    v-if="userIsDm"
                    @click="() => (isHidden = !isHidden)"
                    type="button"
                    variant="outline"
                    class="interactable">
                    <FontAwesomeIcon :icon="isHidden ? faEyeSlash : faEye" />
                    {{ isHidden ? "Hidden" : "Visible" }}
                </Button>
            </div>

            <CampaignCharacterHealthInput
                :health="health!"
                @update:health="(v) => (health = v)"
                :error="healthInputProps.errorMessage"
                :allowRoll="false" />

            <CampaignCharacterArmourClassInput
                v-model:ac="armourClass"
                :error="armourClassInputProps.errorMessage" />

            <!-- <CharacterConditionsInput v-model:conditions="conditions" /> -->
        </main>
        <footer class="flex justify-between">
            <AsyncButton
                name="Save"
                label="Save"
                loadingLabel="Saving..."
                :isLoading="
                    (submitting && submitting.submitterName == 'Save') ?? false
                " />
            <AsyncButton
                name="Delete"
                :icon="faTrash"
                :isLoading="
                    (submitting && submitting.submitterName == 'Delete') ??
                    false
                " />
        </footer>
    </FormBase>
</template>
<script setup lang="ts">
    import { useForm } from "vee-validate";
    import {
        unevaluatedCharacterHealthValidator,
        conditionValidator,
        type InitiativeCharacter,
        type CharacterInitiative,
        type CharacterHealth,
    } from "~/utils/types/models";
    import type { CombatCharacterDto } from "~/utils/api/combat/putUpdateInitiativeCharacterRequest";
    import { toTypedSchema } from "@vee-validate/zod";
    import { z } from "zod";
    import HealthInput from "../Character/HealthInput.vue";
    import {
        faEye,
        faEyeSlash,
        faTrash,
    } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import type { SubmittingState } from "~/components/Form/Base.vue";
    import { mappedHealthInputValidator } from "~/utils/forms/healthFormValidator";

    const characterHealthInput = ref<InstanceType<typeof HealthInput> | null>(
        null
    );
    const userStore = useUserStore();
    const { userIsDm } = storeToRefs(useCombatStore());

    const props = withDefaults(
        defineProps<{
            character: InitiativeCharacter;
        }>(),
        {}
    );

    // Form Definition
    const formState = reactive({
        error: null as ApiError<{ name: string; hidden: boolean }> | null,
    });

    const { values, errors, defineField, validate } = useForm({
        validationSchema: toTypedSchema(
            z
                .object({
                    name: z.string({ required_error: "Please provide a name" }),
                    isHidden: z.boolean(),
                    armourClass: z.number().nullable(),
                    health: mappedHealthInputValidator,
                    conditions: z.array(conditionValidator),
                })
                .required({ name: true, health: true })
        ),
        initialValues: {
            name: props.character.name,
            isHidden: props.character.hidden,
            armourClass: props.character.armourClass ?? null,
            health: props.character.health,
            conditions: props.character.conditions,
        },
    });

    const [name, nameInputProps] = defineField("name", {
        props: (state) => ({
            errorMessage:
                formState.error?.getErrorFor("name") ?? state.errors[0],
        }),
    });

    const [isHidden, isHiddenInputProps] = defineField("isHidden");

    const [health, healthInputProps] = defineField("health", {
        props: (state) => ({
            errorMessage: formState.error?.getErrorFor("character.Initiative"),
        }),
    });
    const [armourClass, armourClassInputProps] = defineField("armourClass");

    const [conditions, conditionsInputProps] = defineField("conditions");

    function setValuesFromProps() {
        if (props.character == null) {
            name.value = "";
            isHidden.value = false;
            return;
        }
        name.value = props.character.name;
        isHidden.value = props.character.hidden;
        armourClass.value = props.character.armourClass ?? null;
        health.value = props.character.health;
        conditions.value = props.character.conditions;
    }
    onMounted(setValuesFromProps);
    watch(() => props.character.id, setValuesFromProps);

    async function submit(formSubmittingState: SubmittingState) {
        if (formSubmittingState.submitterName == "Delete") {
            await onDelete();
        }

        if (formSubmittingState.submitterName == "Save") {
            await onEdit();
        }
    }

    async function onEdit() {
        formState.error = null;

        // Fetch & Set the computed health values from the health component upon submission
        const computedHealth = characterHealthInput.value?.getHealth();
        if (computedHealth == false) {
            return;
        }
        health.value = computedHealth;

        const validateResult = await validate();
        if (!validateResult.valid) {
            return;
        }

        return await props
            .onEdit({
                id: props.character.id,
                name: name.value!,
                hidden: !userIsDm.value ? false : isHidden.value!,
                health: computedHealth! as CharacterHealth,
                initiative: props.character.initiative,
                armourClass: armourClass.value ?? null,
                conditions: conditions.value!,
            })
            .catch((error) => (formState.error = parseAsApiError(error)));
    }
    async function onDelete() {
        return await props
            .onDelete(props.character.id)
            .catch((error) => (formState.error = parseAsApiError(error)));
    }
</script>
