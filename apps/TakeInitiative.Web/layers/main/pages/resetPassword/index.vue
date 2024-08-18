<template>
    <section class="w-full">
        <div class="flex w-full flex-col justify-center">
            <h1 class="text-center text-xl">Reset Your password</h1>
            <NuxtLink
                :to="{
                    path: '/login',
                    query: redirectToPath ? { redirectTo: redirectToPath } : {},
                }"
                class="text-center text-sm underline"
            >
                Back to login</NuxtLink
            >
        </div>

        <FormBase
            class="flex flex-col gap-4"
            v-slot="{ submitting }"
            :onSubmit="onLogin"
            v-if="!state.sentResetEmail"
        >
            <FormInput
                v-model:value="email"
                label="Email"
                type="email"
                placeholder="example@email.com"
                v-bind="emailInputProps"
            />

            <div v-if="state.errorObject" class="text-take-red">
                {{ state.errorObject.getErrorFor("generalErrors") }}
            </div>

            <!-- {{ state.errorObject }} -->

            <div class="flex justify-center">
                <FormButton
                    label="Send Reset Email"
                    type="submit"
                    :loadingDisplay="{
                        loadingText: 'Sending...',
                        showSpinner: true,
                    }"
                    :isLoading="submitting"
                />
            </div>
        </FormBase>

        <div v-else>
            If there exists an account with the given email, a password reset
            email has been sent.
        </div>
    </section>
</template>

<script setup lang="ts">
import { useForm } from "vee-validate";
import { toTypedSchema } from "@vee-validate/yup";
import * as yup from "yup";
import { getDefaultLibFileName } from "typescript";
import type { LocationQueryValue } from "vue-router";
import {
    SendResetPasswordEmailRequestValidator,
    type SendResetPasswordEmailRequest,
} from "base/utils/api/user/putSendResetPasswordRequest";
const redirectToPath = useRoute().query.redirectTo as LocationQueryValue;
const state = reactive({
    errorObject: null as null | ApiError<SendResetPasswordEmailRequest>,
    sentResetEmail: false,
});

definePageMeta({
    requiresAuth: false,
    layout: "auth",
});

// Form Definition
const { values, errors, defineField, validate } = useForm({
    validationSchema: toTypedSchema(SendResetPasswordEmailRequestValidator),
});
const [email, emailInputProps] = defineField("email", {
    props: (_state) => ({
        errorMessage:
            state.errorObject?.getErrorFor("Email") ?? _state.errors[0],
    }),
});

// Form Submit
async function onLogin() {
    state.errorObject = null;
    const validation = await validate();
    if (validation.valid == false) {
        return;
    }

    await useApi()
        .user.sendResetPasswordEmail(email.value!)
        .then(() => {
            state.sentResetEmail = true;
        })
        .catch(
            (e) =>
                (state.errorObject =
                    parseAsApiError<SendResetPasswordEmailRequest>(e)),
        );
}
</script>
