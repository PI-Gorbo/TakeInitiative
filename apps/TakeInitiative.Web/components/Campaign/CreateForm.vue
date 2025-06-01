<template>
    <AutoForm :onSubmit="onSubmit" :schema="createCampaignValidator" :form="form" v-slot="{ isSubmitting }">
        <ErrorPanel v-if="formState.formError?.errors?.generalErrors">
            {{ formState.formError?.errors?.generalErrors.at(0) }}
        </ErrorPanel>
        <AsyncButton label="Create" loadingLabel="Creating..." :isLoading="isSubmitting" type="submit" />
    </AutoForm>
</template>

<script setup lang="ts">
import { useForm } from "vee-validate";
import type { CreateCampaignRequest } from "~/utils/api/campaign/createCampaignRequest";
import { toTypedSchema } from "@vee-validate/zod";
import { z } from "zod";
const formState = reactive({
    formError: null as ApiError<CreateCampaignRequest> | null,
});

const props = defineProps<{
    submit: (req: CreateCampaignRequest) => Promise<any>;
}>();

const createCampaignValidator = z
    .object({
        campaignName: z.string(),
    })
    .required()

const form = useForm({
    validationSchema: toTypedSchema(
        createCampaignValidator
    ),
});

async function onSubmit(req: z.infer<typeof createCampaignValidator>): Promise<void> {
    formState.formError = null;
    return await props
        .submit({ campaignName: req.campaignName })
        .catch((e) => (formState.formError = parseAsApiError(e)));
}
</script>
