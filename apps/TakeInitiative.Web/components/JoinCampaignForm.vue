<template>
    <Form
        v-slot="{ isSubmitting }"
        @submit="onSubmit"
        @submit.prevent="true"
        class="flex flex-col gap-2"
    >
        <FormInput
            label="Join Code"
            v-model:value="joinCode"
            v-bind="joinCodeInputProps"
        />
        <div class="w-full flex justify-center">
            <FormButton
                label="Join"
                loadingLabel="Joining..."
                :isLoading="isSubmitting"
                buttonColour="take-yellow-dark"
            />
        </div>
    </Form>
</template>

<script setup lang="ts">
import { Form } from "vee-validate";
import { toTypedSchema } from "@vee-validate/yup";
import { useForm } from "vee-validate";
import * as yup from "yup";

// Form Definition
const { values, errors, defineField, validate } = useForm({
    validationSchema: toTypedSchema(
        yup.object({
            joinCode: yup.string().required(),
        })
    ),
});
const [joinCode, joinCodeInputProps] = defineField("joinCode", {
    props: (state) => ({
        errorMessage: state.errors[0],
    }),
});

async function onSubmit(): Promise<void> {}
</script>
