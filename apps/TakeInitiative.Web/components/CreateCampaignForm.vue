<template>
    <form @submit="onSubmit" @submit.prevent="true" class="flex flex-col gap-2">
        <FormInput
            label="Campaign name"
            v-model:value="campaignName"
            v-bind="campaignNameInputProps"
        />
        <div class="flex w-full justify-center">
            <FormButton
                label="Create"
                loadingLabel="Creating..."
                :isLoading="formState.isSubmitting"
                buttonColour="take-yellow-dark"
            />
        </div>
    </form>
</template>

<script setup lang="ts">
import { Form } from "vee-validate";
import { toTypedSchema } from "@vee-validate/yup";
import { useForm } from "vee-validate";
import * as yup from "yup";

// Form Definition
const formState = reactive({
    isSubmitting: false,
});
const { values, errors, defineField, validate } = useForm({
    validationSchema: toTypedSchema(
        yup.object({
            campaignName: yup.string().required(),
        }),
    ),
});
const [campaignName, campaignNameInputProps] = defineField("campaignName", {
    props: (state) => ({
        errorMessage: state.errors[0],
    }),
});

async function onSubmit(): Promise<void> {
    formState.isSubmitting = true;
    const validateResult = await validate();
    if (!validateResult.valid) {
        return Promise.resolve();
    }

    await useUserStore()
        .createCampaign({
            campaignName: campaignName.value ?? "",
        })
        .then((campaign) => {
            debugger;
            // Set the campaign as the current campaign
        })
        .then(async () => {
            console.log("here");
            await navigateTo("/");
        })
        .finally(() => {
            formState.isSubmitting = false;
        });
}
</script>
