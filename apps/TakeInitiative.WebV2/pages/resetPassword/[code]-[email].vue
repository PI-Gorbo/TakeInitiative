<template>
    <Card>
        <CardHeader class="flex w-full flex-col justify-center">
            <CardTitle>Reset Password</CardTitle>
        </CardHeader>

        <CardContent>
            <FormBase
                class="flex flex-col gap-4"
                v-slot="{ submitting }"
                :onSubmit="submit">
                <FormFieldWrapper label="Password">
                    <Input
                        v-model="password"
                        type="password"
                        v-bind="passwordInputProps" />
                </FormFieldWrapper>
                <FormFieldWrapper label="Confirm password">
                    <Input
                        v-model="confirmPassword"
                        type="password"
                        v-bind="confirmPasswordProps" />
                </FormFieldWrapper>
                <div
                    v-if="formState.submitError?.errors?.generalErrors"
                    class="text-take-red">
                    {{ formState.submitError?.errors?.generalErrors[0] }}
                </div>
                <div class="flex justify-center">
                    <AsyncButton
                        label="Reset"
                        loadingLabel="Resetting..."
                        :icon="faArrowCircleRight"
                        :isLoading="!!submitting" />
                </div>
            </FormBase>
        </CardContent>
    </Card>
</template>

<script setup lang="ts">
    import { toTypedSchema } from "@vee-validate/zod";
    import type { SignUpRequest } from "~/utils/api/user/signUpRequest";
    import { useForm } from "vee-validate";
    import { z } from "zod";
    import { faArrowCircleRight } from "@fortawesome/free-solid-svg-icons";
    const route = useRoute();

    definePageMeta({
        requiresAuth: false,
        layout: "logo",
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
                formState.submitError?.errors?.password ?? state.errors[0],
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

        if (route.name == "resetPassword-code-email") {
            await useApi()
                .user.resetPasswordWithToken({
                    email: route.params.email,
                    password: password.value!,
                    token: route.params.code,
                })
                .then(async () => await useNavigator().toLogin())
                .catch((e) => (formState.submitError = parseAsApiError(e)));
        }
    }
</script>
