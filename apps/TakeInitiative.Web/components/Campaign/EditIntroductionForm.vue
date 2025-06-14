<template>
    <div>
        <FormFieldWrapper
            label="Intro"
            :error="form.errors.value.description"
            :description="
                !campaignQuery.data.value?.userCampaignMember.isDungeonMaster ||
                !!description
                    ? ''
                    : 'This introduction won\'t be visible to players until you\'ve filled it out'
            ">
            <template #Header>
                <AsyncSuccessIcon :state="debouncedFunc.state.value" />
            </template>
            <template #default
                ><Textarea
                    v-model="description"
                    :readonly="
                        !campaignQuery.data.value?.userCampaignMember
                            .isDungeonMaster
                    "
            /></template>
        </FormFieldWrapper>
    </div>
</template>
<script setup lang="ts">
    import { faCheck, faCircleNotch } from "@fortawesome/free-solid-svg-icons";
    import { FontAwesomeIcon } from "@fortawesome/vue-fontawesome";
    import { useQuery } from "@tanstack/vue-query";
    import { toTypedSchema } from "@vee-validate/zod";
    import { useDebounceFn } from "@vueuse/core";
    import { useForm } from "vee-validate";
    import { toast } from "vue-sonner";
    import { z } from "zod";
    import {
        getCampaignQuery,
        updateCampaignDetailsMutation,
    } from "~/utils/queries/campaign";

    const route = useRoute("app-campaigns-campaignId");
    const campaignQuery = useQuery(
        getCampaignQuery(() => route.params.campaignId)
    );
    watch(
        () => campaignQuery.data.value?.campaign.campaignDescription,
        (newDescription) => {
            if (newDescription) {
                form.setFieldValue("description", newDescription);
            }
        }
    );

    const schema = z.object({
        description: z.string().max(512).min(0),
    });
    const form = useForm({
        validationSchema: toTypedSchema(schema),
        initialValues: {
            description:
                campaignQuery.data.value?.campaign?.campaignDescription ?? "",
        },
    });
    const [description] = form.defineField("description");

    const updateCampaignDetails = updateCampaignDetailsMutation();
    async function submitDetails(
        req: z.infer<typeof schema>
    ): Promise<unknown> {
        return await updateCampaignDetails
            .mutateAsync({
                campaignId: route.params.campaignId,
                campaignDescription: req.description,
            })
            .then(() => {
                toast.success("Saved Introduction");
            });
    }

    const debouncedFunc = useDebouncedAsyncFn(
        async (req: z.infer<typeof schema>) => await submitDetails(req)
    );

    watch(
        () => form.values.description,
        (newDescription) => {
            if (!campaignQuery.data.value?.userCampaignMember.isDungeonMaster) {
                return;
            }
            
            if (!form.isFieldValid("description")) {
                return;
            }
            debouncedFunc.debouncedSubmit({
                description: newDescription ?? "",
            });
        }
    );
</script>
