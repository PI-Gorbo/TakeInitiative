<template>
    <section class="w-full">
        <div class="w-full flex flex-col justify-center">
            <h1 class="text-xl text-center">Log In</h1>
            <NuxtLink to="/signup" class="underline text-sm text-center">
                Sign up instead</NuxtLink
            >
        </div>

        <Form class="flex flex-col gap-4" @submit.prevent @submit="onLogin">
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

            <div class="flex justify-center">
                <button
                    class="w-full bg-take-yellow my-2 py-4 rounded-md hover:brightness-75 text-take-navy"
                    type="submit"
                >
                    Login
                </button>
            </div>
        </Form>
    </section>
</template>

<script setup lang="ts">
import { Form } from "vee-validate";
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
            email: yup.string().required().email(),
            password: yup.string().required(),
        })
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
async function onLogin() {
    const validation = await validate();
    if (validation.valid == false) {
        return;
    }
}
</script>
