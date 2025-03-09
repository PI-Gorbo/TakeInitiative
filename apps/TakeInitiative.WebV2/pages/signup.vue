<template>
    <Card>
        <CardHeader class="flex flex-row justify-between">
            <CardTitle>
                <h2>Sign Up</h2>
            </CardTitle>
            <CardDescription>
                <NuxtLink :to="{
                        path: '/login',
                        query: redirectToPath
                            ? { redirectTo: redirectToPath }
                            : {},
                    }" class="text-center text-sm underline">
                    Login instead
                </NuxtLink>
            </CardDescription>
        </CardHeader>

        <CardContent>
            <AutoForm :schema="registerSchema" :form="form" :onSubmit="onSignUp" :fieldConfig="{
                    password: {
                        inputProps: {
                            type: 'password',
                        },
                    },
                    confirmPassword: {
                        inputProps: {
                            type: 'password',
                        },
                    },
                }" v-slot="{ isSubmitting }">
                <Transition name="fade" mode="out-in">
                    <div v-if="!passwordEnteredCorrectly"
                        class="rounded-md border bg-accent p-2 text-accent-foreground">
                        <label class="text-sm italic">Password Requirements</label>
                        <section class="grid grid-flow-row grid-cols-1 lg:grid-cols-2">
                            <div>
                                <FontAwesomeIcon :class="[
                                        validPasswordLength
                                            ? 'text-gold'
                                            : 'text-destructive',
                                    ]" :icon="
                                        validPasswordLength ? faCheck : faXmark
                                    " />
                                6 or more characters
                            </div>
                            <div>
                                <FontAwesomeIcon :class="[
                                        validPasswordHasLower
                                            ? 'text-gold'
                                            : 'text-destructive',
                                    ]" :icon="
                                        validPasswordHasLower
                                            ? faCheck
                                            : faXmark
                                    " />
                                At least one lowercase character
                            </div>
                            <div>
                                <FontAwesomeIcon :class="[
                                        validPasswordHasUpper
                                            ? 'text-gold'
                                            : 'text-destructive',
                                    ]" :icon="
                                        validPasswordHasUpper
                                            ? faCheck
                                            : faXmark
                                    " />
                                At least one uppercase character
                            </div>
                            <div>
                                <FontAwesomeIcon :class="[
                                        validPasswordHasDigit
                                            ? 'text-gold'
                                            : 'text-destructive',
                                    ]" :icon="
                                        validPasswordHasDigit
                                            ? faCheck
                                            : faXmark
                                    " />
                                At least one digit
                            </div>
                            <div>
                                <FontAwesomeIcon :class="[
                                        validPasswordHasSpecial
                                            ? 'text-gold'
                                            : 'text-destructive',
                                    ]" :icon="
                                        validPasswordHasSpecial
                                            ? faCheck
                                            : faXmark
                                    " />
                                At least one special character
                            </div>
                            <div>
                                <FontAwesomeIcon :class="[
                                        passwordsMatch
                                            ? 'text-gold'
                                            : 'text-destructive',
                                    ]" :icon="
                                        passwordsMatch ? faCheck : faXmark
                                    " />
                                Passwords match
                            </div>
                        </section>
                    </div>
                    <div v-else class="flex flex-col gap-2 pt-2">
                        <div class="flex justify-center">
                            <AsyncButton type="submit" label="Sign Up" loadingLabel="Signing up..."
                                :isLoading="isSubmitting" :icon="faArrowRight" />
                        </div>
                    </div>
                </Transition>
            </AutoForm>
        </CardContent>

        <CardFooter class="flex justify-end">
            <NuxtLink :to="{
                    path: '/resetPassword',
                    query: redirectToPath ? { redirectTo: redirectToPath } : {},
                }" class="text-center text-sm underline">
                Forgot password</NuxtLink>
        </CardFooter>
    </Card>
</template>

<script setup lang="ts">
    import { useForm } from "vee-validate";
    import type { LocationQueryValue } from "vue-router";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import type { SignUpRequest } from "~/utils/api/user/signUpRequest";
    import { toTypedSchema } from "@vee-validate/zod";
    import { z } from "zod";
    import {
        faArrowRight,
        faCheck,
        faXmark,
    } from "@fortawesome/free-solid-svg-icons";
    const redirectToPath = useRoute().query.redirectTo as LocationQueryValue;
    definePageMeta({
        requiresAuth: false,
        layout: "logo",
    });

    // Form Definition
    const formState = reactive({
        submitError: null as ApiError<SignUpRequest> | null,
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
        .required();

    const form = useForm({ validationSchema: toTypedSchema(registerSchema) });

    // Form Submit
    const userStore = useUserStore();
    async function onSignUp(request: z.infer<typeof registerSchema>) {
        formState.submitError = null; // Reset the submit error.

        if (request.password !== request.confirmPassword) {
            form.setFieldError("confirmPassword", "Passwords do not match.");
            return;
        }

        return await userStore
            .signUp({
                email: request.email,
                username: request.username,
                password: request.password,
            })
            .catch((error) => {
                formState.submitError = parseAsApiError<SignUpRequest>(error);
                form.setErrors({
                    username: formState.submitError.errors?.username,
                    email: formState.submitError.errors?.email,
                    password: formState.submitError.errors?.password,
                })
            })
            .then(async () => {
                if (redirectToPath != null) {
                    return await navigateTo(redirectToPath);
                } else {
                    return await useNavigator().toCampaignsList();
                }
            })

    }

    // Computed Form Password display
    const validPasswordLength = computed(() =>
        testPasswordLength(form.values.password ?? "")
    );
    const validPasswordHasLower = computed(() =>
        testPasswordHasLower(form.values.password ?? "")
    );
    const validPasswordHasUpper = computed(() =>
        testPasswordHasUpper(form.values.password ?? "")
    );
    const validPasswordHasDigit = computed(() =>
        testPasswordHasDigit(form.values.password ?? "")
    );
    const validPasswordHasSpecial = computed(() =>
        testPasswordHasSpecial(form.values.password ?? "")
    );
    const passwordsMatch = computed(
        () =>
            (form.values.password != "" || form.values.password != null) &&
            form.values.confirmPassword === form.values.password
    );

    const passwordEnteredCorrectly = computed(
        () =>
            validPasswordLength.value &&
            validPasswordHasLower.value &&
            validPasswordHasUpper.value &&
            validPasswordHasDigit.value &&
            validPasswordHasSpecial.value &&
            passwordsMatch.value
    );
</script>
