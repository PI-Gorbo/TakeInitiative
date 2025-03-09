<template>
    <Card>
        <CardHeader class="flex flex-row justify-between">
            <CardTitle><h2>Log In</h2></CardTitle>
            <CardDescription>
                <NuxtLink
                    :to="{
                        path: '/signup',
                        query: redirectToPath
                            ? { redirectTo: redirectToPath }
                            : {},
                    }"
                    class="text-center text-sm underline">
                    Sign up instead</NuxtLink
                >
            </CardDescription>
        </CardHeader>

        <CardContent>
            <AutoForm
                :schema="loginFormValidator"
                :onSubmit="onLogin"
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
                        v-if="state.errorObject"
                        class="rounded-md border-destructive bg-destructive/50 p-2 text-sm text-destructive-foreground">
                        {{ state.errorObject.getErrorFor("generalErrors") }}
                    </div>
                    <div class="flex justify-center">
                        <AsyncButton
                            type="submit"
                            label="Login"
                            loadingLabel="Logging in..."
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
                    query: redirectToPath ? { redirectTo: redirectToPath } : {},
                }"
                class="text-center text-sm underline">
                Forgot password</NuxtLink
            >
        </CardFooter>
    </Card>
</template>

<script setup lang="ts">
    import type { LoginRequest } from "~/utils/api/user/loginRequest";
    import type { LocationQueryValue } from "vue-router";
    import { z } from "zod";
    import AsyncButton from "~/components/AsyncButton.vue";
    import { faArrowRight } from "@fortawesome/free-solid-svg-icons";
    const redirectToPath = useRoute().query.redirectTo as LocationQueryValue;

    const state = reactive({
        errorObject: null as null | ApiError<LoginRequest>,
    });

    definePageMeta({
        requiresAuth: false,
    });

    // Form Definition
    const loginFormValidator = z
        .object({
            email: z.string().email(),
            password: z.string(),
        })
        .required();

    // Form Submit
    const userStore = useUserStore();
    async function onLogin(request: z.infer<typeof loginFormValidator>) {
        state.errorObject = null;

        const result = await userStore
            .login(request)
            .then(async () => {
                if (redirectToPath != null) {
                    await navigateTo(redirectToPath);
                } else {
                    await userStore.navigateToFirstAvailableCampaignOrFallbackToCreateOrJoin();
                }
            })
            .catch((error) => {
                debugger;
                state.errorObject = parseAsApiError(error);
            });
    }
</script>
