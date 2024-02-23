<template>
    <section class="w-full">
        <div class="flex w-full flex-col justify-center">
            <h1 class="text-center text-xl">Log In</h1>
            <NuxtLink to="/signup" class="text-center text-sm underline">
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
                    loadingDisplay="Logging in..."
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
const state = reactive({
    isSubmitting: false,
    errorObject: null as null | ApiError<LoginRequest>,
});

definePageMeta({
    requiresAuth: false,
    layout: "auth",
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

    await userStore
        .login({ email: email.value ?? "", password: password.value ?? "" })
        .then(async () => {
            await navigateTo("/");
        })
        .catch(async (error) => {
            state.errorObject = await parseAsApiError(error);
        })
        .finally(() => (state.isSubmitting = false));
}
</script>
