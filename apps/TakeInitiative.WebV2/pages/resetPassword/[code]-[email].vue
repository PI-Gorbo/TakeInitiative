<template>
    <section class="w-full">
        <div class="flex w-full flex-col justify-center">
            <h1 class="text-center text-xl">Reset Password</h1>
        </div>

        <FormBase
            class="flex flex-col gap-4"
            v-slot="{ submitting }"
            :onSubmit="submit">
            <FormInput
                v-model:value="password"
                label="Password"
                type="password"
                v-bind="passwordInputProps" />
            <FormInput
                v-model:value="confirmPassword"
                label="Confirm password"
                type="password"
                v-bind="confirmPasswordProps" />

            <div
                v-if="formState.submitError?.errors?.generalErrors"
                class="text-take-red">
                {{ formState.submitError?.errors?.generalErrors[0] }}
            </div>

            <div class="flex justify-center">
                <FormButton
                    label="Reset"
                    :loadingDisplay="{
                        showSpinner: true,
                        loadingText: 'Resetting...',
                    }"
                    :isLoading="submitting" />
            </div>
        </FormBase>
    </section>
</template>

<script setup lang="ts">
    import { toTypedSchema } from "@vee-validate/zod";
    import type { SignUpRequest } from "~/utils/api/user/signUpRequest";
    import { useForm } from "vee-validate";
    import { z } from "zod";
    const route = useRoute();

    definePageMeta({
        requiresAuth: false,
    });

    // Form Definition
    const formState = reactive({
        submitError: null as ApiError<SignUpRequest> | null,
    });
    const { values, errors, defineField, validate } = useForm({
        validationSchema: toTypedSchema(
            z
                .object({
                    password: z.string(),
                    confirmPassword: z.string({
                        required_error: "Please confirm your password",
                    }),
                })
                .required()
                .refine(
                    (obj) => obj.password == obj.confirmPassword,
                    "Passwords do not match."
                )
        ),
    });

    const [password, passwordInputProps] = defineField("password", {
        props: (state) => ({
            errorMessage:
                formState.submitError?.getErrorObjectFor("password") ??
                state.errors[0],
        }),
    });
    const [confirmPassword, confirmPasswordProps] = defineField(
        "confirmPassword",
        {
            props: (state) => ({
                errorMessage: state.errors[0],
            }),
        }
    );

    // Form Submit
    const userStore = useUserStore();
    async function submit() {
        formState.submitError = null; // Reset the submit error.
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
            .catch((e) => (formState.submitError = parseAsApiError(e)));
    }
</script>
