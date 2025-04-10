<template>
    <FormFieldWrapper
        label="Intro"
        :error="form.errors.value.description"
        description="This introduction won't be visible to players until you've filled it out">
        <template #Header>
            <AsyncSuccessIcon :state="debouncedFunc.state.value" />
        </template>
        <template #default><Textarea v-model="description" /></template>
    </FormFieldWrapper>
</template>
<script setup lang="ts">
    import { faCheck, faCircleNotch } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import { toTypedSchema } from "@vee-validate/zod";
    import { useDebounceFn } from "@vueuse/core";
    import { useForm } from "vee-validate";
    import { toast } from "vue-sonner";
    import { z } from "zod";

    const campaignStore = useCampaignStore();

    const schema = z.object({
        description: z.string().max(512).min(0),
    });
    const form = useForm({
        validationSchema: toTypedSchema(schema),
        initialValues: {
            description:
                campaignStore.state.campaign?.campaignDescription ?? "",
        },
    });
    const [description] = form.defineField("description");

    async function submitDetails(
        req: z.infer<typeof schema>
    ): Promise<unknown> {
        return await campaignStore
            .updateCampaignDetails({
                campaignDescription: req.description,
            })
            .then(() => {
                toast.success("Saved Introduction");
            });
    }

    const debouncedFunc = useDebouncedAsyncFn(
        async (req: z.infer<typeof schema>) => await submitDetails(req),
        1000,
        4000
    );

    watch(
        () => form.values.description,
        (newDescription) => {
            if (!form.isFieldValid("description")) {
                return;
            }
            debouncedFunc.debouncedSubmit({
                description: newDescription ?? "",
            });
        }
    );
</script>
