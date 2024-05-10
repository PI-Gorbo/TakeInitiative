<template>
    <section class="w-full">
        <div class="flex w-full flex-col justify-center">
            <h1 class="text-center text-xl">Sign Up</h1>
            <NuxtLink to="/login" class="text-center text-sm underline">
                Login instead</NuxtLink
            >
        </div>

        <form class="flex flex-col gap-4" @submit.prevent @submit="onSignUp">
            <FormInput
                v-model:value="email"
                label="Email"
                type="email"
                placeholder="example@email.com"
                v-bind="emailInputProps"
            />
            <FormInput
                v-model:value="username"
                label="Username"
                type="input"
                placeholder="username"
                v-bind="usernameInputProps"
            />
            <FormInput
                v-model:value="password"
                label="Password"
                type="password"
                v-bind="passwordInputProps"
            />
            <FormInput
                v-model:value="confirmPassword"
                label="Confirm password"
                type="password"
                v-bind="confirmPasswordProps"
            />

            <div v-if="formState.submitError?.errors?.generalErrors" class="text-take-red">
                {{ formState.submitError?.errors?.generalErrors[0] }}
            </div>

            <div v-if="formState.success" class="text-take-yellow">Signing in...</div>

            <div class="flex justify-center">
                <FormButton
                    label="Sign Up"
                    :loadingDisplay="{
                        showSpinner: true,
                        loadingText: 'Signing Up...'
                    }"
                    :isLoading="formState.submitting"
                />
            </div>
        </form>
    </section>
</template>

<script setup lang="ts">
import { Form } from "vee-validate";
import { useForm } from "vee-validate";
import { toTypedSchema } from "@vee-validate/yup";
import * as yup from "yup";
import { formatDiagnosticsWithColorAndContext } from "typescript";
import type { SignUpRequest } from "~/utils/api/user/signUpRequest";
definePageMeta({
    requiresAuth: false,
    layout: "auth",
});

// Form Definition
const formState = reactive({
    submitError: null as ApiError<SignUpRequest> | null,
    success: false,
    submitting: false as boolean,
});
const { values, errors, defineField, validate } = useForm({
    validationSchema: toTypedSchema(
        yup.object({
            username: yup.string().required(),
            email: yup.string().required().email(),
            password: yup.string().required(),
            confirmPassword: yup
                .string()
                .required("Please confirm your password")
                .test("matches password", function (value) {
                    const { path, createError } = this;
                    if (value != password.value) {
                        createError({
                            path,
                            message: "Passwords do not match.",
                        });
                    }
                    return true;
                }),
        })
    ),
});
const [email, emailInputProps] = defineField("email", {
    props: (state) => {
        return {
            errorMessage: formState?.submitError?.getErrorFor("email") ?? state.errors[0],
        };
    },
});
const [username, usernameInputProps] = defineField("username", {
    props: (state) => ({
        errorMessage: formState.submitError?.getErrorFor("username") ?? state.errors[0],
    }),
});
const [password, passwordInputProps] = defineField("password", {
    props: (state) => ({
        errorMessage: formState.submitError?.getErrorFor("password") ?? state.errors[0],
    }),
});
const [confirmPassword, confirmPasswordProps] = defineField("confirmPassword", {
    props: (state) => ({
        errorMessage: state.errors[0],
    }),
});

// Form Submit
const userStore = useUserStore();
async function onSignUp() {
    formState.submitError = null; // Reset the submit error.
    formState.submitting = true;
    const validation = await validate();
    if (validation.valid == false) {
        formState.submitting = false;
        return;
    }

    await userStore
        .signUp({
            email: email.value!,
            username: username.value!,
            password: password.value!,
        })
        .then(() => (formState.success = true))
        .catch(async (error) => {
            try {
                formState.submitError = await parseAsApiError<SignUpRequest>(error);
            } catch {
                formState.submitError = {
                    // TODO : Refactor
                    statusCode: 500,
                    message: "Something went wrong while trying to sign in.",
                    errors: {
                        generalErrors: ["Something went wrong while trying to sign in."],
                    },
                    getErrorFor: (error) =>
                        error == "generalErrors"
                            ? "Something went wrong while trying to sign in."
                            : null,
                };
            }
        })
        .finally(() => (formState.submitting = false));
}
</script>
