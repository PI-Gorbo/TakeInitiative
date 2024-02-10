<template>
    <section class="w-full">
        <div class="w-full flex flex-col justify-center">
            <h1 class="text-xl text-center">Sign Up</h1>
            <NuxtLink to="/login" class="underline text-sm text-center">
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

            <div class="flex justify-center">
                <button
                    class="w-full bg-take-red my-2 py-4 rounded-md hover:brightness-75 text-white"
                    type="submit"
                >
                    Sign Up
                </button>
            </div>
        </form>
    </section>
</template>

<script setup lang="ts">
import { useForm } from "vee-validate";
import { toTypedSchema } from "@vee-validate/yup";
import * as yup from "yup";
definePageMeta({
    requiresAuth: false,
    layout: "auth",
});

// Form Definition
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
                        createError({ path, message: "Passwords do not match." });
                    }
                    return true;
                }),
        })
    ),
});
const [email, emailInputProps] = defineField("email", {
    props: (state) => ({
        errorMessage: state.errors[0],
    }),
});
const [username, usernameInputProps] = defineField("username", {
    props: (state) => ({
        errorMessage: state.errors[0],
    }),
});
const [password, passwordInputProps] = defineField("password", {
    props: (state) => ({
        errorMessage: state.errors[0],
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
    const validation = await validate();
    if (validation.valid == false) {
        return;
    }

    await userStore
        .signUp(email.value!, username.value!, password.value!)
        .catch((error) => {
            console.log("THERE WAS AN ERROR", error);
        });
}
</script>
