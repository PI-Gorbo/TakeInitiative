<template>
    <FormBase
        class="flex flex-col gap-2"
        :onSubmit="onSubmit"
        v-slot="{ submitting }"
    >
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
            :value="quantity"
            @update:value="(val) => (quantity = Number(val) ?? 1)"
            v-bind="quantityInputProps"
        />

        <section>
            <label class="text-white">Initiative</label>
            <div class="flex flex-row">
                <select
                    name="Initiative Strategy"
                    :value="initiativeStrategy"
                    @input="
                        (e: Event) =>
                            (initiativeStrategy = (
                                e.target as HTMLSelectElement
                            ).value)
                    "
                    class="rounded-l-lg bg-take-grey-dark py-1 pl-2 pr-1"
                >
                    <option :value="InitiativeStrategy.Fixed">Fixed</option>
                    <option :value="InitiativeStrategy.Roll">Roll</option>
                </select>

                <input
                    type="text"
                    class="flex-1 rounded-r-lg bg-take-navy-light px-1 text-white"
                    :value="initiativeValue"
                    @input="
                        (e) =>
                            (initiativeValue = (e.target as HTMLInputElement)
                                .value)
                    "
                    :placeholder="
                        initiativeStrategy == InitiativeStrategy.Fixed
                            ? '+5'
                            : '1d20 + 5'
                    "
                />
            </div>
            <label
                v-if="initiativeValueInputProps.errorMessage != null"
                class="text-take-red"
            >
                {{ initiativeValueInputProps.errorMessage }}
            </label>
        </section>

        <div class="flex w-full justify-center">
            <FormButton
                label="Create"
                loadingDisplay="Creating..."
                :isLoading="submitting"
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
    type PlannedCombatNonPlayerCharacter,
    type PlannedCombatStage,
} from "~/utils/types/models";

const props = defineProps<{
    stage: PlannedCombatStage;
    onSubmit: (
        stage: PlannedCombatStage,
        plannedCombatNonPlayerCharacter: PlannedCombatNonPlayerCharacter,
    ) => Promise<void>;
}>();

// Form Definition
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
    quantity.value = 1;
});

async function onSubmit() {
    const validateResult = await validate();
    if (!validateResult.valid) {
        return;
    }

    return await props.onSubmit({
        ...values,
    } as PlannedCombatNonPlayerCharacter);
}

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
