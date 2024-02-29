<template>
    <FormBase class="flex flex-col gap-2" :onSubmit="onSubmit" v-slot="{ submitting }">
        <FormInput
            textColour="white"
            label="Name"
            v-model:value="name"
            v-bind="nameInputProps"
            :autoFocus="true"
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
                            (initiativeStrategy = Number((
                                e.target as HTMLSelectElement
                            ).value))
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
                        initiativeStrategy == InitiativeStrategy.Fixed ? '+5' : '1d20 + 5'
                    "
                />
            </div>
            <label
                v-if="initiativeStrategyInputProps.errorMessage"
                class="text-take-red"
                >{{ initiativeStrategyInputProps.errorMessage }}</label
            >
            <label
                v-if="initiativeValueInputProps.errorMessage != null"
                class="text-take-red"
            >
                {{ initiativeValueInputProps.errorMessage }}
            </label>
        </section>

        <div class="flex w-full justify-center">
            <FormButton
                label="Save"
                loadingDisplay="Saving..."
                :isLoading="submitting"
                buttonColour="take-yellow-dark"
            />
            <FormButton
                label="Delete"
                loadingDisplay="Delete..."
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
characterInitiativeValidator,
} from "~/utils/types/models";
import type { CreatePlannedCombatNpcRequest } from "~/utils/api/plannedCombat/stages/npcs/createPlannedCombatNpcRequest";

const formState = reactive({
    error: null as ApiError<CreatePlannedCombatNpcRequest> | null,
});

const props = defineProps<{
    onSubmit: (
        request: Omit<CreatePlannedCombatNpcRequest, "combatId" | "stageId">,
    ) => Promise<void>;
}>();

// Form Definition
const { values, errors, defineField, validate } = useForm({
    validationSchema: toTypedSchema(
        yup.object({
            name: yup.string().required("Please provide a name"),
            initiative: characterInitiativeValidator,
            quantity: yup.number().min(1),
        }),
    ),
});
const [name, nameInputProps] = defineField("name", {
    props: (state) => ({
        errorMessage: formState.error?.getErrorFor("name") ?? state.errors[0],
    }),
});

const [quantity, quantityInputProps] = defineField("quantity", {
    props: (state) => ({
        errorMessage:
            formState.error?.getErrorFor("quantity") ?? state.errors[0],
    }),
});

const [initiativeStrategy, initiativeStrategyInputProps] = defineField(
    "initiative.strategy",
    {
        props: (state) => ({
            errorMessage:
                formState.error?.getErrorFor("initiative.strategy") ??
                state.errors[0],
        }),
    },
);

const [initiativeValue, initiativeValueInputProps] = defineField(
    "initiative.value",
    {
        props: (state) => ({
            errorMessage:
                formState.error?.getErrorFor("initiative.value") ??
                state.errors[0],
        }),
    },
);

onMounted(() => {
    initiativeStrategy.value = InitiativeStrategy.Roll;
    initiativeValue.value = "1d20 + 1";
    quantity.value = 1;
});

async function onSubmit() {

	await new Promise((resolve) => setTimeout(resolve ,() => {
	console.log("Delayed for 1 second.");
	}, 1000))



    // formState.error = null;
    // const validateResult = await validate();
    // if (!validateResult.valid) {
    //     console.log(validateResult);
    //     return;
    // }

    // console.log("Submitting!");
    // return await props
    //     .onSubmit({
    //         health: null,
    //         initiative: {
    //             strategy: initiativeStrategy.value!,
    //             value: initiativeValue.value!,
    //         },
    //         name: name.value!,
    //         quantity: quantity.value!,
    //         armourClass: null,
    //     })
    //     .catch(async (error) => {
    //         formState.error = await parseAsApiError(error);
    //     });
}

async function onDelete() {

}
</script>
