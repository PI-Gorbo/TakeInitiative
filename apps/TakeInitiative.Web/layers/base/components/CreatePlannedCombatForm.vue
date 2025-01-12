<template>
    <FormBase
        :onSubmit="submit"
        v-slot="{ submitting }"
        class="flex w-1/2 flex-col gap-2 pb-2">
        <FormInput
            class="text-white"
            label="Combat Name"
            v-bind="createPlannedCombatForm.combatName.props.value"
            :value="createPlannedCombatForm.combatName.value.value"
            @update:value="
                (val) =>
                    (createPlannedCombatForm.combatName.value.value =
                        String(val) ?? '')
            "
            colour="take-navy-light"
            :autoFocus="true" />

        <div class="text-sm text-take-red">
            {{ createPlannedCombatForm.errors.value.combatName }}
        </div>

        <div class="flex gap-2 justify-end">
            <FormButton
                :isLoading="
                    submitting && submitting.submitterName == 'Create & Plan'
                "
                label="Create & Plan"
                type="submit"
                :loadingDisplay="{
                    showSpinner: true,
                    loadingText: 'Creating...',
                }" />
            <FormButton
                :isLoading="
                    submitting &&
                    submitting.submitterName == 'Open Combat to Players'
                "
                label="Open Combat to Players"
                type="submit"
                button-colour="take-cream"
                :loadingDisplay="{
                    showSpinner: true,
                    loadingText: 'Creating...',
                }"
                :disabled="currentCampaign.hasOpenCombat" />
        </div>
    </FormBase>
</template>

<script setup lang="ts">
    // TODO : Refactor to be the smae as the CreateCampaignForm
    import type { SubmittingState } from "base/components/Form/Base.vue";

    type FormInputProps = InstanceType<typeof FormInput>["$props"];
    import FormInput from "base/components/Form/Input.vue";
    import type { CreatePlannedCombatRequest } from "base/utils/api/plannedCombat/createPlannedCombatRequest";

    const currentCampaign = useCampaignStore();
    const createPlannedCombatForm = useCreatePlannedCombatForm();

    const props = defineProps<{
        onCreatePlannedCombat: (
            input: void | Omit<CreatePlannedCombatRequest, "campaignId">,
            startCombatImmediately: boolean
        ) => Promise<any>;
    }>();

    const emit = defineEmits<{
        (e: "update:campaignName", campaignName: string | undefined): void;
    }>();

    function submit(
        submitState: SubmittingState
    ): ReturnType<typeof createPlannedCombatForm.submit> {
        return createPlannedCombatForm
            .submit(submitState.submitterName == "Open Combat to Players")
            .then((req) =>
                props.onCreatePlannedCombat(
                    req,
                    submitState.submitterName == "Open Combat to Players"
                )
            )
            .catch((error) =>
                createPlannedCombatForm.setError(parseAsApiError(error))
            );
    }
</script>
