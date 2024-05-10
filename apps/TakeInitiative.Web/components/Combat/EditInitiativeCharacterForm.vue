<template>
    <FormBase :onSubmit="submit" v-slot="{ submitting }">
        <main class="pb-2">
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
    characterInitiativeValidator,
    type CombatCharacter,
} from "~/utils/types/models";
const { userIsDm } = storeToRefs(useCombatStore());

const props = withDefaults(
    defineProps<{
        character: CombatCharacter;
        onEdit?: (request: {}) => Promise<any>;
        onDelete?: (request: Omit<unknown, "combatId">) => Promise<any>;
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

function setValuesFromProps() {
    name.value = props.character.name;
    isHidden.value = props.character.hidden;
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

async function onEdit() {}
async function onDelete() {}
</script>
