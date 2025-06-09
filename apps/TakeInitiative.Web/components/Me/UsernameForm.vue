<template>
    <Card class="p-2">
        <form @submit.prevent="onSubmit">
            <FormFieldWrapper
                label="Username"
                :error="form.errors.value.newUsername">
                <div class="flex justify-between items-center gap-2">
                    <Input
                        type="text"
                        v-model="name"
                        :disabled="!isEditable"
                        class="disabled:opacity-100 disabled:bg-muted"
                        ref="input" />
                    <template v-if="isEditable">
                        <Button
                            variant="outline"
                            class="interactable"
                            :class="{
                                'fa-spin': form.isSubmitting.value,
                            }"
                            size="icon"
                            type="submit">
                            <FontAwesomeIcon
                                :icon="
                                    form.isSubmitting.value ? faSpinner : faSave
                                " />
                        </Button>
                    </template>
                    <template v-else>
                        <Button
                            variant="outline"
                            class="interactable"
                            size="icon"
                            @click="onToggle">
                            <FontAwesomeIcon :icon="faPencil" />
                        </Button>
                    </template>
                </div>
            </FormFieldWrapper>
        </form>
    </Card>
</template>

<script setup lang="ts">
    import {
        faPencil,
        faSave,
        faSpinner,
    } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import { toTypedSchema } from "@vee-validate/zod";
    import { useForm } from "vee-validate";
    import { toast } from "vue-sonner";
    import { z } from "zod";
    import { useUpdateUsernameMutation } from "~/utils/queries/user";
    const input = useTemplateRef("input");
    const userStore = useUserStore();
    const form = useForm({
        validationSchema: toTypedSchema(
            z.object({
                newUsername: z.string().nonempty().min(3),
            })
        ),
        initialValues: {
            newUsername: userStore.state?.username,
        },
    });
    const [name] = form.defineField("newUsername");

    const isEditable = ref(false);
    function onToggle() {
        isEditable.value = !isEditable.value;
    }

    // Focus the input, after it has been un-disabled
    watch(
        isEditable,
        (value) => {
            if (value) {
                (input.value?.$el as HTMLInputElement).focus();
            }
        },
        {
            flush: "post",
        }
    );

    const updateUsername = useUpdateUsernameMutation();
    const onSubmit = form.handleSubmit(async (formValue) => {
        await updateUsername
            .mutateAsync({
                newUsername: formValue.newUsername,
            })
            .then(() => {
                toast.success("Updated username");
                form.resetForm({
                    values: {
                        newUsername: formValue.newUsername,
                    },
                });
                isEditable.value = false;
            })
            .catch((err) => {
                const extractedError = parseAsApiError(err);
                toast.error(`Failed to update username`);
                form.setErrors({
                    newUsername: extractedError.errors?.generalErrors?.at(0),
                });
            });
    });
</script>
