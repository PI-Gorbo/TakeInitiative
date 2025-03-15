<template>
    <AutoForm
        :form="form"
        :schema="schema"
        :fieldConfig="{
            description: {
                label: 'Intro',
                description:
                    'This introduction won\'t be visible to players untill you\'ve filled it out',
                component: 'textarea',
            },
        }" />
</template>
<script setup lang="ts">
    import { toTypedSchema } from "@vee-validate/zod";
    import { useDebounceFn } from "@vueuse/core";
    import { useForm } from "vee-validate";
    import { z } from "zod";

    const state = reactive({
        isSubmitting: false,
    });

    const schema = z.object({
        description: z.string().max(512).min(0),
    });
    const form = useForm({ validationSchema: toTypedSchema(schema) });

    const campaignStore = useCampaignStore();
    async function submitDetails(
        req: z.infer<typeof schema>
    ): Promise<unknown> {
        return await campaignStore.updateCampaignDetails({
            campaignDescription: req.description,
        });
    }
    const debouncedSubmit = useDebounceFn(
        async (req: z.infer<typeof schema>) => await submitDetails(req),
        500
    );

    watch(
        () => form.values.description,
        (newDescription) => {
            if (!form.isFieldValid("description")) {
                return;
            }
            state.isSubmitting = true;
            debouncedSubmit({ description: newDescription ?? "" });
        }
    );
</script>
