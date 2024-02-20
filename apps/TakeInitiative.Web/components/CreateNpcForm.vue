<template>
    <FormBase class="flex flex-col gap-2" :onSubmit="onSubmit">
        <FormInput
            textColour="white"
            label="Name"
            v-model:value="name"
            v-bind="nameInputProps"
        />
        <FormInput
            label="Quantity"
            textColour="white"
            type="number"
            v-model:value="quantity"
            v-bind="quantityInputProps"
        />

        <section>
            <label>Initiative</label>
            <div class="flex flex-row">
                <select
                    name="Initiative Strategy"
                    :value="initiativeStrategy"
                    class="rounded-l-xl bg-take-grey-dark py-1 pl-2 pr-1"
                >
                    <option :value="InitiativeStrategy.Fixed">Fixed</option>
                    <option :value="InitiativeStrategy.Roll">Roll</option>
                </select>

                <input
                    type="text"
                    class="flex-1 rounded-r-xl bg-take-navy-light px-1 text-white"
                    :value="initiativeValue"
                />
            </div>
        </section>

        <div class="flex w-full justify-center">
            <FormButton
                label="Create"
                textColour="white"
                loadingDisplay="Creating..."
                :isLoading="formState.isSubmitting"
                buttonColour="take-yellow-dark"
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
    plannedCombatNonPlayerCharacterValidator,
} from "~/utils/types/models";

// Form Definition
const formState = reactive({
    isSubmitting: false,
});
const { values, errors, defineField, validate } = useForm({
    validationSchema: toTypedSchema(plannedCombatNonPlayerCharacterValidator),
});
const [name, nameInputProps] = defineField("name", {
    props: (state) => ({
        errorMessage: state.errors[0],
    }),
});

const [quantity, quantityInputProps] = defineField("quantity", {
    props: (state) => ({
        errorMessage: state.errors[0],
    }),
});

const [initiativeStrategy, initiativeStrategyInputProps] = defineField(
    "initiative.strategy",
    {
        props: (state) => ({
            errorMessage: state.errors[0],
        }),
    },
);

const [initiativeValue, initiativeValueInputProps] = defineField(
    "initiative.value",
    {
        props: (state) => ({
            errorMessage: state.errors[0],
        }),
    },
);

onMounted(() => {
    initiativeStrategy.value = InitiativeStrategy.Roll;
    initiativeValue.value = "1d20 + 1";
});

async function onSubmit() {}

// async function onSubmit(): Promise<void> {
//     formState.isSubmitting = true;
//     const validateResult = await validate();
//     if (!validateResult.valid) {
//         return Promise.resolve();
//     }

//     await useUserStore()
//         .createCampaign({
//             campaignName: campaignName.value ?? "",
//         })
//         .then((campaign) => {
//             // Set the campaign as the current campaign
//         })
//         .then(async () => {
//             await navigateTo("/");
//         })
//         .finally(() => {
//             formState.isSubmitting = false;
//         });
// }
</script>
