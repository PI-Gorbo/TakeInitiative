<template>
    <AutoForm :onSubmit="onSubmit" v-slot="{ isSubmitting }" :schema="joinValidator" :form="form">
        <ErrorPanel v-if="formState.formError?.errors?.generalErrors?.length">
            {{ formState.formError?.errors?.generalErrors.at(0) }}
        </ErrorPanel>
        <AsyncButton type="submit" label="Join" loadingLabel="Joining..." :isLoading="isSubmitting" />
    </AutoForm>
</template>

<script setup lang="ts">
import { toTypedSchema } from "@vee-validate/zod";
import type { JoinCampaignRequest } from "~/utils/api/campaign/joinCampaignRequest";
import { useForm } from "vee-validate";
import { z } from "zod";

const formState = reactive({
    formError: null as null | ApiError<JoinCampaignRequest>,
});

const props = defineProps<{
    submit: (req: JoinCampaignRequest) => Promise<any>;
}>();

// Form Definition
const joinValidator = z
    .object({
        joinCode: z.string(),
    })
    .required()

const form = useForm({
    validationSchema: toTypedSchema(
        joinValidator
    ),
});
async function onSubmit(req: z.infer<typeof joinValidator>): Promise<void> {
    formState.formError = null;
    return await props
        .submit({
            joinCode: req.joinCode!
        })
        .catch((e) => (formState.formError = parseAsApiError(e)));
}
</script>
