<template>
    <section class="w-full">
        <div class="flex w-full flex-col justify-center">
            <h1 class="text-center text-xl">Log In</h1>
            <NuxtLink
                :to="{
                    path: '/signup',
                    query: redirectToPath ? { redirectTo: redirectToPath } : {},
                }"
                class="text-center text-sm underline"
            >
                Sign up instead</NuxtLink
            >
        </div>

        <form class="flex flex-col gap-4" @submit.prevent @submit="onLogin">
            <FormInput
                v-model:value="email"
                label="Email"
                type="email"
                placeholder="example@email.com"
                v-bind="emailInputProps"
            />
            <FormInput
                v-model:value="password"
                label="Password"
                type="password"
                v-bind="passwordInputProps"
            />

            <div v-if="state.errorObject" class="text-take-red">
                {{ state.errorObject.getErrorFor("generalErrors") }}
            </div>

            <div class="flex justify-center">
                <FormButton
                    label="Login"
                    type="submit"
                    :loadingDisplay="{
                        loadingText: 'Logging in...',
                        showSpinner: true,
                    }"
                    :isLoading="state.isSubmitting"
                />
            </div>
        </form>
    </section>
</template>

<script setup lang="ts">
import { useForm } from "vee-validate";
import { toTypedSchema } from "@vee-validate/yup";
import * as yup from "yup";
import type { LoginRequest } from "~/utils/api/user/loginRequest";
import { getDefaultLibFileName } from "typescript";
import type { LocationQueryValue } from "vue-router";
const redirectToPath = useRoute().query.redirectTo as LocationQueryValue;
const state = reactive({
    isSubmitting: false,
    errorObject: null as null | ApiError<LoginRequest>,
});

definePageMeta({
    requiresAuth: false,
    layout: "auth",
    middleware: [
        async (to) => {
            const userStore = useUserStore();
            const isLoggedIn = await userStore.isLoggedIn();
            if (redirectToPath != null) {
                await navigateTo(redirectToPath);
            } else {
                await userStore.navigateToFirstAvailableCampaign();
            }

            return;
        },
    ],
});

// Form Definition
const { values, errors, defineField, validate } = useForm({
    validationSchema: toTypedSchema(
        yup.object({
            email: yup.string().required().email(),
            password: yup.string().required(),
        }),
    ),
});
const [email, emailInputProps] = defineField("email", {
    props: (state) => ({
        errorMessage: state.errors[0],
    }),
});
const [password, passwordInputProps] = defineField("password", {
    props: (state) => ({
        errorMessage: state.errors[0],
    }),
});

// Form Submit
const userStore = useUserStore();
async function onLogin() {
    state.errorObject = null;
    state.isSubmitting = true;
    const validation = await validate();
    if (validation.valid == false) {
        state.isSubmitting = false;
        return;
    }

    console.log("Logging in...");
    await userStore
        .login({ email: email.value ?? "", password: password.value ?? "" })
        .catch(async (error) => {
            state.errorObject = await parseAsApiError(error);
        })
        .then(async () => {
            if (redirectToPath != null) {
                console.log("using redirect path");
                await navigateTo(redirectToPath);
            } else {
                console.log("navigating to first available campaign");
                await userStore.navigateToFirstAvailableCampaign();
            }
        })
        .finally(() => (state.isSubmitting = false));
}
</script>
