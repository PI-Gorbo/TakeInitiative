<template>
    <Card>
        <CardHeader class="flex w-full flex-col justify-center">
            <CardTitle class="flex justify-between"
                ><span>Reset Your password</span>
                <CardDescription>
                    <NuxtLink
                        :to="{
                            path: '/login',
                            query: redirectToPath
                                ? { redirectTo: redirectToPath }
                                : {},
                        }"
                        class="text-sm underline">
                        Back to login
                    </NuxtLink>
                </CardDescription></CardTitle
            >
        </CardHeader>

        <CardContent>
            <FormBase
                v-if="!state.sentResetEmail"
                class="flex flex-col gap-4"
                v-slot="{ submitting }"
                :onSubmit="onLogin">
                <FormFieldWrapper
                    label="Email"
                    :error="emailInputProps.errorMessage">
                    <Input
                        v-model="email"
                        type="email"
                        placeholder="example@email.com" />
                </FormFieldWrapper>
                <div
                    v-if="state.errorObject"
                    class="text-take-red">
                    {{ state.errorObject.errors?.generalErrors }}
                </div>
                <div class="flex justify-center">
                    <AsyncButton
                        type="submit"
                        :icon="faMailForward"
                        label="Send Reset Email"
                        loadingLabel="Sending..."
                        :isLoading="!!submitting" />
                </div>
            </FormBase>
            <div v-else>
                If there exists an account with the given email, a password
                reset email has been sent.
            </div>
        </CardContent>
    </Card>
</template>

<script setup lang="ts">
    import { useForm } from "vee-validate";
    import type { LocationQueryValue } from "vue-router";
    import {
        SendResetPasswordEmailRequestValidator,
        type SendResetPasswordEmailRequest,
    } from "~/utils/api/user/putSendResetPasswordRequest";
    import { toTypedSchema } from "@vee-validate/zod";
    import { faMailForward } from "@fortawesome/free-solid-svg-icons";
    const redirectToPath = useRoute().query.redirectTo as LocationQueryValue;
    const state = reactive({
        errorObject: null as null | ApiError<SendResetPasswordEmailRequest>,
        sentResetEmail: false,
    });

    definePageMeta({
        requiresAuth: false,
        layout: "logo",
    });

    // Form Definition
    const { values, errors, defineField, validate } = useForm({
        validationSchema: toTypedSchema(SendResetPasswordEmailRequestValidator),
    });
    const [email, emailInputProps] = defineField("email", {
        props: (_state) => ({
            errorMessage: state.errorObject?.errors?.email?.at(0) ?? _state.errors[0],
        }),
    });

    // Form Submit
    async function onLogin() {
        state.errorObject = null;
        const validation = await validate();
        if (validation.valid == false) {
            return;
        }

        await useApi()
            .user.sendResetPasswordEmail(email.value!)
            .then(() => {
                state.sentResetEmail = true;
            })
            .catch(
                (e) =>
                    (state.errorObject =
                        parseAsApiError<SendResetPasswordEmailRequest>(e))
            );
    }
</script>
