<template>
    <section class="w-full">
        <div class="flex w-full flex-col justify-center">
            <h1 class="text-center text-xl">Reset Password</h1>
        </div>

        <FormBase
            class="flex flex-col gap-4"
            v-slot="{ submitting }"
            :onSubmit="submit"
        >
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

            <div
                v-if="formState.submitError?.errors?.generalErrors"
                class="text-take-red"
            >
                {{ formState.submitError?.errors?.generalErrors[0] }}
            </div>

            <div class="flex justify-center">
                <FormButton
                    label="Reset"
                    :loadingDisplay="{
                        showSpinner: true,
                        loadingText: 'Resetting...',
                    }"
                    :isLoading="submitting"
                />
            </div>
        </FormBase>
    </section>
</template>

<script setup lang="ts">
import { Form } from "vee-validate";
import { useForm } from "vee-validate";
import { toTypedSchema } from "@vee-validate/yup";
import * as yup from "yup";
import { formatDiagnosticsWithColorAndContext } from "typescript";
import type { SignUpRequest } from "base/utils/api/user/signUpRequest";
import type { LocationQueryValue } from "vue-router";
const route = useRoute();
definePageMeta({
    requiresAuth: false,
    layout: "auth",
});

// Form Definition
const formState = reactive({
    submitError: null as ApiError<SignUpRequest> | null,
});
const { values, errors, defineField, validate } = useForm({
    validationSchema: toTypedSchema(
        yup.object({
            password: yup.string().required(),
            confirmPassword: yup
                .string()
                .required("Please confirm your password")
                .test("matches password", function (value) {
                    const { path, createError } = this;
                    if (value != password.value) {
                        return createError({
                            path,
                            message: "Passwords do not match.",
                        });
                    }
                    return true;
                }),
        }),
    ),
});

const [password, passwordInputProps] = defineField("password", {
    props: (state) => ({
        errorMessage:
            formState.submitError?.getErrorFor("password") ?? state.errors[0],
    }),
});
const [confirmPassword, confirmPasswordProps] = defineField("confirmPassword", {
    props: (state) => ({
        errorMessage: state.errors[0],
    }),
});

// Form Submit
const userStore = useUserStore();
async function submit() {
    formState.submitError = null; // Reset the submit error.
    console.log(password.value, confirmPassword.value);
    const validation = await validate();
    if (validation.valid == false) {
        return;
    }

    await useApi()
        .user.resetPasswordWithToken({
            email: route.params.email as string,
            password: password.value!,
            token: route.params.code as string,
        })
        .then(async () => await useNavigator().toLogin())
        .catch(async (e) => (formState.submitError = await parseAsApiError(e)));
}
</script>
