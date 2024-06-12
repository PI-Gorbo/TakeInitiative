<template>
    <FormBase
        @submit="onSubmit"
        v-slot="{ submitting }"
        class="flex flex-col gap-2"
    >
        <FormInput
            label="Campaign name"
            v-model:value="campaignName"
            v-bind="campaignNameInputProps"
            :autoFocus="true"
        />
        <div
            v-if="
                formState.formError &&
                formState.formError.getErrorFor('generalErrors')
            "
        >
            <label class="text-take-red">
                {{ formState.formError.getErrorFor("generalErrors") }}
            </label>
        </div>
        <div class="flex w-full justify-center">
            <FormButton
                label="Create"
                :loadingDisplay="{
                    loadingText: 'Creating...',
                    showSpinner: true,
                }"
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
import type { CreateCampaignRequest } from "../layers/base/utils/api/campaign/createCampaignRequest";
const formState = reactive({
    formError: null as null | ApiError<CreateCampaignRequest>,
});

const props = defineProps<{
    submit: (req: CreateCampaignRequest) => Promise<any>;
}>();

const { values, errors, defineField, validate } = useForm({
    validationSchema: toTypedSchema(
        yup.object({
            campaignName: yup.string().required(),
        }),
    ),
});
const [campaignName, campaignNameInputProps] = defineField("campaignName", {
    props: (state) => ({
        errorMessage:
            formState.formError == null
                ? state.errors[0]
                : formState.formError?.getErrorFor("campaignName"),
    }),
});

async function onSubmit(): Promise<void> {
    formState.formError = null;
    const validateResult = await validate();
    if (!validateResult.valid) {
        return Promise.resolve();
    }
    return await props
        .submit({ campaignName: campaignName.value ?? "" })
        .catch(async (e) => (formState.formError = await parseAsApiError(e)));
}
</script>
