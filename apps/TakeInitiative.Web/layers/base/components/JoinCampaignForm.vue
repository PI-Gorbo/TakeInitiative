<template>
    <FormBase
        v-slot="{ submitting }"
        :onSubmit="onSubmit"
        class="flex flex-col gap-2"
    >
        <FormInput
            label="Join Code"
            v-model:value="joinCode"
            v-bind="joinCodeInputProps"
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
                type="submit"
                label="Join"
                :loadingDisplay="{
                    showSpinner: true,
                    loadingText: 'Joining...',
                }"
                :isLoading="submitting"
                buttonColour="take-yellow-dark"
            />
        </div>
    </FormBase>
</template>

<script setup lang="ts">
import { toTypedSchema } from "@vee-validate/yup";
import { useForm } from "vee-validate";
import * as yup from "yup";
import type { JoinCampaignRequest } from "../layers/base/utils/api/campaign/joinCampaignRequest";

const formState = reactive({
    formError: null as null | ApiError<JoinCampaignRequest>,
});

const props = defineProps<{
    submit: (req: JoinCampaignRequest) => Promise<any>;
}>();

// Form Definition
const { values, errors, defineField, validate } = useForm({
    validationSchema: toTypedSchema(
        yup.object({
            joinCode: yup.string().required(),
        }),
    ),
});
const [joinCode, joinCodeInputProps] = defineField("joinCode", {
    props: (state) => ({
        errorMessage: formState.formError
            ? formState.formError.getErrorFor("joinCode")
            : state.errors[0],
    }),
});

async function onSubmit(): Promise<void> {
    formState.formError = null;
    return await props
        .submit({ joinCode: joinCode.value! })
        .catch((e) => (formState.formError = parseAsApiError(e)));
}
</script>
