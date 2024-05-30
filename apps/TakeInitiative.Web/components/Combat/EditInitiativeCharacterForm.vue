<template>
    <FormBase :onSubmit="submit" v-slot="{ submitting }">
        <main class="pb-2" v-if="props.character">
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
                    v-if="
                        userIsDm &&
                        props.character.playerId == userStore.state.user?.userId
                    "
                    :icon="isHidden ? 'eye-slash' : 'eye'"
                    size="lg"
                    :label="isHidden ? 'Hidden' : 'Visible'"
                    @clicked="() => (isHidden = !isHidden)"
                    buttonColour="take-navy-light"
                    type="button"
                />
            </div>

            <CharacterHealth
                v-model:hasHealth="hasHealth"
                v-model:currentHealth="currentHealth"
                v-model:maxHealth="maxHealth"
                :error="
                    hasHealthInputProps.errorMessage ??
                    currentHealthInputProps.errorMessage ??
                    maxHealthInputProps.errorMessage
                "
            />

            <CharacterArmourClass v-model:value="armourClass" />
        </main>
        <footer class="flex justify-between">
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
        </footer>
    </FormBase>
</template>
<script setup lang="ts">
import { useForm } from "vee-validate";
import type { SubmittingState } from "../Form/Base.vue";
import { toTypedSchema } from "@vee-validate/yup";
import { yup } from "~/utils/types/HelperTypes";
import {
    characterHealthValidator,
    characterInitiativeValidator,
    type CombatCharacter,
} from "~/utils/types/models";
import type { CombatCharacterDto } from "~/utils/api/combat/putUpdateInitiativeCharacterRequest";
const userStore = useUserStore();
const { userIsDm } = storeToRefs(useCombatStore());

const props = withDefaults(
    defineProps<{
        character: CombatCharacter;
        onEdit: (character: CombatCharacterDto) => Promise<any>;
        onDelete: (characterId: string) => Promise<any>;
    }>(),
    {},
);

// Form Definition
const formState = reactive({
    error: null as ApiError<{ name: string; hidden: boolean }> | null,
});

const { values, errors, defineField, validate } = useForm({
    validationSchema: toTypedSchema(
        yup.object({
            name: yup.string().required("Please provide a name"),
            isHidden: yup.boolean(),
            armourClass: yup.number().nullable(),
            health: characterHealthValidator.required(),
        }),
    ),
});

const [name, nameInputProps] = defineField("name", {
    props: (state) => ({
        errorMessage: formState.error?.getErrorFor("name") ?? state.errors[0],
    }),
});

const [isHidden, isHiddenInputProps] = defineField("isHidden", {
    props: (state) => ({
        errorMessage: formState.error?.getErrorFor("hidden") ?? state.errors[0],
    }),
});

const [hasHealth, hasHealthInputProps] = defineField("health.hasHealth", {
    props: (state) => ({
        errorMessage:
            (formState.error?.getErrorFor("playerCharacter.Health.HasHealth") ||
                formState.error?.getErrorFor("in")) ??
            state.errors[0],
    }),
});

const [currentHealth, currentHealthInputProps] = defineField(
    "health.currentHealth",
    {
        props: (state) => ({
            errorMessage:
                formState.error?.getErrorFor(
                    "playerCharacter.Health.CurrentHealth",
                ) ?? state.errors[0],
        }),
    },
);

const [maxHealth, maxHealthInputProps] = defineField("health.maxHealth", {
    props: (state) => ({
        errorMessage:
            formState.error?.getErrorFor("playerCharacter.Health.MaxHealth") ??
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

function setValuesFromProps() {
    if (props.character == null) {
        name.value = "";
        isHidden.value = false;
        return;
    }
    name.value = props.character.name;
    isHidden.value = props.character.hidden;
    armourClass.value = props.character.armourClass ?? null;
    hasHealth.value = props.character.health?.hasHealth ?? false;
    currentHealth.value = props.character.health?.currentHealth ?? 0;
    maxHealth.value = props.character.health?.maxHealth ?? 0;
}
onMounted(setValuesFromProps);
watch(() => props.character, setValuesFromProps);

async function submit(formSubmittingState: SubmittingState) {
    if (formSubmittingState.submitterName == "trash") {
        await onDelete();
    }

    if (formSubmittingState.submitterName == "Save") {
        await onEdit();
    }
}

async function onEdit() {
    return await props
        .onEdit({
            id: props.character.id,
            name: name.value!,
            hidden: isHidden.value!,
            initiativeValue: props.character.initiativeValue!,
            health: {
                hasHealth: hasHealth.value ?? false,
                currentHealth: currentHealth.value ?? 0,
                maxHealth: maxHealth.value ?? 0,
            },
            armourClass: armourClass.value ?? null,
        })
        .catch(
            async (error) => (formState.error = await parseAsApiError(error)),
        );
}
async function onDelete() {
    return await props
        .onDelete(props.character.id)
        .catch(
            async (error) => (formState.error = await parseAsApiError(error)),
        );
}
</script>
