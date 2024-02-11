<template>
    <Form
        v-slot="{ isSubmitting }"
        @submit="onSubmit"
        @submit.prevent="true"
        class="flex flex-col gap-2"
    >
        <FormInput
            label="Campaign name"
            v-model:value="campaignName"
            v-bind="campaignNameInputProps"
        />
        <div class="w-full flex justify-center">
            <FormButton
                label="Create"
                loadingLabel="Creating..."
                :isLoading="isSubmitting"
                buttonColour="take-yellow-dark"
            />
        </div>
    </Form>
</template>

<script setup lang="ts">
import { Form, validate } from "vee-validate";
import { toTypedSchema } from "@vee-validate/yup";
import { useForm } from "vee-validate";
import * as yup from "yup";

// Form Definition
const { values, errors, defineField, validate } = useForm({
    validationSchema: toTypedSchema(
        yup.object({
            campaignName: yup.string().required(),
        })
    ),
});
const [campaignName, campaignNameInputProps] = defineField("campaignName", {
    props: (state) => ({
        errorMessage: state.errors[0],
    }),
});

async function onSubmit(): Promise<void> {
    const validateResult = await validate();
    if (!validateResult.valid) {
        return Promise.resolve();
    }

	const createCampaignResult = await useUserStore()
		.
}
</script>
