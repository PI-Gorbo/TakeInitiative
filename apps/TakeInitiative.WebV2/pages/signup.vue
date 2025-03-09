<template>
    <div class="flex h-full w-full flex-col items-center pt-20">
        <div class="w-full max-w-2xl sm:w-4/5">
            <header>
                <NuxtLink to="/">
                    <h1
                        class="flex items-center justify-center gap-4 font-NovaCut text-4xl font-bold text-gold sm:text-6xl">
                        <img
                            class="h-[2em] w-[2em]"
                            src="~/public/img/yellowDice.png" />
                        Take Initiative
                    </h1>
                </NuxtLink>
            </header>
            <Card>
                <CardHeader class="flex flex-row justify-between">
                    <CardTitle><h2>Sign Up</h2></CardTitle>
                    <CardDescription>
                        <NuxtLink
                            :to="{
                                path: '/login',
                                query: redirectToPath
                                    ? { redirectTo: redirectToPath }
                                    : {},
                            }"
                            class="text-center text-sm underline">
                            Login instead
                        </NuxtLink>
                    </CardDescription>
                </CardHeader>

                <CardContent>
                    <AutoForm
                        :schema="registerSchema"
                        :onSubmit="onSignUp"
                        :fieldConfig="{
                            password: {
                                inputProps: {
                                    type: 'password',
                                },
                            },
                        }"
                        v-slot="{ isSubmitting }">
                        <div class="flex flex-col gap-2 pt-2">
                            <div
                                v-if="formState.submitError"
                                class="rounded-md border-destructive bg-destructive/50 p-2 text-sm text-destructive-foreground">
                                {{
                                    formState.submitError.getErrorFor(
                                        "generalErrors"
                                    )
                                }}
                            </div>
                            <div class="flex justify-center">
                                <AsyncButton
                                    type="submit"
                                    label="Sign Up"
                                    loadingLabel="Signing up..."
                                    :isLoading="isSubmitting"
                                    :icon="faArrowRight" />
                            </div>
                        </div>
                    </AutoForm>
                </CardContent>

                <CardFooter class="flex justify-end">
                    <NuxtLink
                        :to="{
                            path: '/resetPassword',
                            query: redirectToPath
                                ? { redirectTo: redirectToPath }
                                : {},
                        }"
                        class="text-center text-sm underline">
                        Forgot password</NuxtLink
                    >
                </CardFooter>
            </Card>
        </div>
        <!-- <section class="w-full">
            <div class="flex w-full flex-col justify-center">
                <h1 class="text-center text-xl">Sign Up</h1>
                <NuxtLink
                    :to="{
                        path: '/login',
                        query: redirectToPath
                            ? { redirectTo: redirectToPath }
                            : {},
                    }"
                    class="text-center text-sm underline">
                    Login instead</NuxtLink
                >
            </div>

            <form
                class="flex flex-col gap-4"
                @submit.prevent
                @submit="onSignUp">
                <FormInput
                    v-model:value="email"
                    label="Email"
                    type="email"
                    placeholder="example@email.com"
                    v-bind="emailInputProps" />
                <FormInput
                    v-model:value="username"
                    label="Username"
                    type="input"
                    placeholder="username"
                    v-bind="usernameInputProps" />
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

                <div class="border-take-navy-light rounded-md border p-2">
                    <label class="text-sm italic">Password Requirements</label>
                    <section
                        class="grid grid-flow-row grid-cols-1 lg:grid-cols-2">
                        <div>
                            <FontAwesomeIcon
                                :icon="validPasswordLength ? 'check' : 'x'" />
                            6 or more characters
                        </div>
                        <div>
                            <FontAwesomeIcon
                                :icon="validPasswordHasLower ? 'check' : 'x'" />
                            At least one lowercase character
                        </div>
                        <div>
                            <FontAwesomeIcon
                                :icon="validPasswordHasUpper ? 'check' : 'x'" />
                            At least one uppercase character
                        </div>
                        <div>
                            <FontAwesomeIcon
                                :icon="validPasswordHasDigit ? 'check' : 'x'" />
                            At least one digit
                        </div>
                        <div>
                            <FontAwesomeIcon
                                :icon="
                                    validPasswordHasSpecial ? 'check' : 'x'
                                " />
                            At least one special character
                        </div>
                    </section>
                </div>
                <div
                    v-if="formState.submitError?.errors?.generalErrors"
                    class="text-take-red">
                    {{ formState.submitError?.errors?.generalErrors[0] }}
                </div>

                <div v-if="formState.success" class="text-take-yellow">
                    Signing in...
                </div>

                <div class="flex justify-center">
                    <FormButton
                        label="Sign Up"
                        :loadingDisplay="{
                            showSpinner: true,
                            loadingText: 'Signing Up...',
                        }"
                        :isLoading="formState.submitting" />
                </div>
            </form>

            <div class="flex justify-end">
                <NuxtLink
                    :to="{
                        path: '/resetPassword',
                        query: redirectToPath
                            ? { redirectTo: redirectToPath }
                            : {},
                    }"
                    class="text-center text-sm underline">
                    Forgot password</NuxtLink
                >
            </div>
        </section> -->
    </div>
</template>

<script setup lang="ts">
    import { useForm } from "vee-validate";
    import type { LocationQueryValue } from "vue-router";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import type { SignUpRequest } from "~/utils/api/user/signUpRequest";
    import { toTypedSchema } from "@vee-validate/zod";
    import { z } from "zod";
    import { faArrowRight } from "@fortawesome/free-solid-svg-icons";
    const redirectToPath = useRoute().query.redirectTo as LocationQueryValue;
    definePageMeta({
        requiresAuth: false,
    });

    // Form Definition
    const formState = reactive({
        submitError: null as ApiError<SignUpRequest> | null,
        success: false,
        submitting: false as boolean,
    });

    const testPasswordLength = (password: string) => password.length >= 6;
    const testPasswordHasLower = (p: string) => p.match(/[a-z]/) != null;
    const testPasswordHasUpper = (p: string) => p.match(/[A-Z]/) != null;
    const testPasswordHasDigit = (p: string) => p.match(/\d/) != null;
    const testPasswordHasSpecial = (p: string) =>
        p.match(/[^a-zA-Z0-9]/) != null;

    const registerSchema = z
        .object({
            username: z.string(),
            email: z.string().email(),
            password: z
                .string({
                    required_error: "A password is required",
                })
                .refine(
                    testPasswordLength,
                    "Must be at least 6 characters long"
                )
                .refine(
                    testPasswordHasLower,
                    "Must have at least one lowercase character."
                )
                .refine(
                    testPasswordHasUpper,
                    "Must have at least one uppercase character."
                )
                .refine(
                    testPasswordHasDigit,
                    "Must have at least one digit character."
                )
                .refine(
                    testPasswordHasSpecial,
                    "Must have at least one special character."
                ),
            confirmPassword: z.string({
                required_error: "Please confirm your password",
            }),
        })
        .required()
        .refine(
            (obj) => obj.password == obj.confirmPassword,
            "Passwords do not match."
        );
    // const [email, emailInputProps] = defineField("email", {
    //     props: (state) => {
    //         return {
    //             errorMessage:
    //                 formState?.submitError?.getErrorFor("email") ??
    //                 state.errors[0],
    //         };
    //     },
    // });
    // const [username, usernameInputProps] = defineField("username", {
    //     props: (state) => ({
    //         errorMessage:
    //             formState.submitError?.getErrorFor("username") ??
    //             state.errors[0],
    //     }),
    // });
    // const [password, passwordInputProps] = defineField("password", {
    //     props: (state) => ({
    //         errorMessage:
    //             formState.submitError?.getErrorFor("password") ??
    //             state.errors[0],
    //     }),
    // });
    // const [confirmPassword, confirmPasswordProps] = defineField(
    //     "confirmPassword",
    //     {
    //         props: (state) => ({
    //             errorMessage: state.errors[0],
    //         }),
    //     }
    // );

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

        return await userStore
            .signUp({
                email: email.value!,
                username: username.value!,
                password: password.value!,
            })
            .catch((error) => {
                formState.submitError = parseAsApiError<SignUpRequest>(error);
            })
            .then(async () => {
                if (redirectToPath != null) {
                    return await navigateTo(redirectToPath);
                } else {
                    return await userStore.navigateToFirstAvailableCampaignOrFallbackToCreateOrJoin();
                }
            })
            .finally(() => {
                formState.success = true;
                formState.submitting = false;
            });
    }

    // Computed Form Password display
    const validPasswordLength = computed(() =>
        testPasswordLength(password.value ?? "")
    );
    const validPasswordHasLower = computed(() =>
        testPasswordHasLower(password.value ?? "")
    );
    const validPasswordHasUpper = computed(() =>
        testPasswordHasUpper(password.value ?? "")
    );
    const validPasswordHasDigit = computed(() =>
        testPasswordHasDigit(password.value ?? "")
    );
    const validPasswordHasSpecial = computed(() =>
        testPasswordHasSpecial(password.value ?? "")
    );
</script>
